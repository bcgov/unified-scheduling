using Mapster;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Unified.Authorization.Claims;
using Unified.Common.Helpers.Extensions;
using Unified.Db;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Services;

public sealed class RoleService(UnifiedDbContext DB, IHttpContextAccessor httpContextAccessor) : IRoleService
{
    public async Task<IReadOnlyCollection<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var roles = await DB
            .Roles.AsNoTracking()
            .Where(r => r.DeletedById == null)
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return roles.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyCollection<RoleAssignedUserDto>> GetAssignedUsersAsync(
        int roleId,
        CancellationToken cancellationToken = default
    )
    {
        var roleExists = await DB.Roles.AnyAsync(r => r.Id == roleId && r.DeletedById == null, cancellationToken);
        if (!roleExists)
            throw new KeyNotFoundException($"Role {roleId} not found.");

        var now = DateTimeOffset.UtcNow;

        return await DB
            .UserRoles.AsNoTracking()
            .Where(ur =>
                ur.RoleId == roleId && (ur.ExpiryDate == null || ur.ExpiryDate > now)
            )
            .Select(ur => new RoleAssignedUserDto
            {
                UserId = ur.UserId,
                IsEnabled = ur.User.IsEnabled,
                FirstName = ur.User.FirstName,
                LastName = ur.User.LastName,
                Email = ur.User.Email,
            })
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ToListAsync(cancellationToken);
    }

    public async Task<RoleDto> CreateAsync(RoleRequestDto request, CancellationToken cancellationToken = default)
    {
        if (
            await DB.Roles.AnyAsync(
                r => r.DeletedById == null && r.Name.ToLower() == request.Name.ToLower(),
                cancellationToken
            )
        )
            throw new InvalidOperationException($"A role with name '{request.Name}' already exists.");

        var role = new Role { Name = request.Name, Description = request.Description };
        DB.Roles.Add(role);
        await DB.SaveChangesAsync(cancellationToken);

        await AddPermissionsAsync(role, request.PermissionIds, cancellationToken);
        await DB.SaveChangesAsync(cancellationToken);

        await DB.Entry(role)
            .Collection(r => r.RolePermissions)
            .Query()
            .Include(rp => rp.Permission)
            .LoadAsync(cancellationToken);

        return MapToDto(role);
    }

    public async Task<RoleDto> UpdateAsync(UpdateRoleRequestDto request, CancellationToken cancellationToken = default)
    {
        var role =
            await DB
                .Roles.Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.Id == request.Id && r.DeletedById == null, cancellationToken)
            ?? throw new KeyNotFoundException($"Role {request.Id} not found.");

        if (role.ConcurrencyToken != request.ConcurrencyToken)
            throw new InvalidOperationException("The role was modified by another request. Please refresh and retry.");

        if (
            !string.Equals(role.Name, request.Name, StringComparison.OrdinalIgnoreCase)
            && await DB.Roles.AnyAsync(
                r => r.Id != request.Id && r.DeletedById == null && r.Name.ToLower() == request.Name.ToLower(),
                cancellationToken
            )
        )
            throw new InvalidOperationException($"A role with name '{request.Name}' already exists.");

        role.Name = request.Name;
        role.Description = request.Description;

        var requestedIds = request.PermissionIds.ToHashSet();
        var existingIds = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();

        DB.RolePermissions.RemoveRange(role.RolePermissions.Where(rp => !requestedIds.Contains(rp.PermissionId)));

        await AddPermissionsAsync(role, requestedIds.Except(existingIds).ToList(), cancellationToken);
        await DB.SaveChangesAsync(cancellationToken);

        await DB.Entry(role)
            .Collection(r => r.RolePermissions)
            .Query()
            .Include(rp => rp.Permission)
            .LoadAsync(cancellationToken);

        return MapToDto(role);
    }

    public async Task<DeletedRoleDto> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var role =
            await DB.Roles.FirstOrDefaultAsync(r => r.Id == id && r.DeletedById == null, cancellationToken)
            ?? throw new KeyNotFoundException($"Role {id} not found.");
        var currentUserId = httpContextAccessor.HttpContext!.User.CurrentUserId();
        var deletedOn = DateTimeOffset.UtcNow;

        role.DeletedOn = deletedOn;
        role.DeletedById = currentUserId;
        await DB.SaveChangesAsync(cancellationToken);

        return new DeletedRoleDto
        {
            Id = role.Id,
            DeletedBy = currentUserId,
            DeletedOn = deletedOn,
        };
    }

    public async Task<DeletedRoleDto> ReassingAndDeleteAsync(
        int roleIdToDelete,
        DeleteRoleWithReassignmentRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        var currentUserId = httpContextAccessor.HttpContext!.User.CurrentUserId();
        var now = DateTimeOffset.UtcNow;
        
        var roleToDelete =
            await DB.Roles.FirstOrDefaultAsync(
                r => r.Id == roleIdToDelete && r.DeletedById == null,
                cancellationToken
            )
            ?? throw new KeyNotFoundException($"Role {roleIdToDelete} not found.");
        var activeAssignments = await DB.UserRoles.Where(ur =>
            ur.RoleId == roleIdToDelete && (ur.ExpiryDate == null || ur.ExpiryDate > now)
        ).ToListAsync(cancellationToken);

        var activeAssignmentCount = activeAssignments.Count;

        if (activeAssignmentCount > 0)
        {
            if (request.NewRoleId == null)
                throw new InvalidOperationException(
                    $"Cannot delete role {roleIdToDelete} with {activeAssignmentCount} assigned user(s) without specifying a new role."
                );

            if (request.NewRoleId == roleIdToDelete)
                throw new InvalidOperationException("The new role cannot be the same as the role being deleted.");

            var newRoleExists = await DB.Roles.AnyAsync(
                r => r.Id == request.NewRoleId && r.DeletedById == null,
                cancellationToken
            );
            if (!newRoleExists)
                throw new KeyNotFoundException($"New role {request.NewRoleId} not found.");

            if (
                string.IsNullOrWhiteSpace(request.NewRoleEffectiveDate)
                || !DateTimeOffsetExtensions.IsValidDateFormat(request.NewRoleEffectiveDate)
            )
                throw new InvalidOperationException("A valid effective date is required when reassigning users.");

            var timezoneId = httpContextAccessor.HttpContext!.User.HomeLocationTimezone();
            var effectiveDate = DateTimeOffsetExtensions.FromDateStringToStartOfDayInTimeZone(
                request.NewRoleEffectiveDate,
                timezoneId
            );

            DateTimeOffset? expiryDate = null;
            if (!string.IsNullOrWhiteSpace(request.NewRoleExpiryDate))
            {
                if (!DateTimeOffsetExtensions.IsValidDateFormat(request.NewRoleExpiryDate))
                    throw new InvalidOperationException("Invalid expiry date format.");

                expiryDate = DateTimeOffsetExtensions.FromDateStringToEndOfDayInTimeZone(
                    request.NewRoleExpiryDate,
                    timezoneId
                );

                if (expiryDate <= effectiveDate)
                    throw new InvalidOperationException("Expiry date must be after the effective date.");
            }

            await using var transaction = await DB.Database.BeginTransactionAsync(cancellationToken);

            var userIds = activeAssignments.Select(ur => ur.UserId).ToList();

            var existingNewRoleAssignments = await DB
                .UserRoles.Where(ur => ur.RoleId == request.NewRoleId && userIds.Contains(ur.UserId))
                .ToDictionaryAsync(ur => ur.UserId, cancellationToken);

            foreach (var assignment in activeAssignments)
            {
                if (existingNewRoleAssignments.TryGetValue(assignment.UserId, out var existingAssignment))
                {
                    if (existingAssignment.EffectiveDate > now && existingAssignment.EffectiveDate != effectiveDate)
                        existingAssignment.EffectiveDate = effectiveDate;

                    existingAssignment.ExpiryDate = expiryDate;
                    existingAssignment.ExpiryReason = null;
                    continue;
                }

                DB.UserRoles.Add(
                    new UserRole
                    {
                        UserId = assignment.UserId,
                        RoleId = request.NewRoleId,
                        EffectiveDate = effectiveDate,
                        ExpiryDate = expiryDate,
                        ExpiryReason = null,
                    }
                );
            }

            DB.UserRoles.RemoveRange(activeAssignments);

            roleToDelete.DeletedOn = now;
            roleToDelete.DeletedById = currentUserId;
            await DB.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new DeletedRoleDto
            {
                Id = roleToDelete.Id,
                DeletedBy = currentUserId,
                DeletedOn = now,
            };
        }

        roleToDelete.DeletedOn = now;
        roleToDelete.DeletedById = currentUserId;
        await DB.SaveChangesAsync(cancellationToken);

        return new DeletedRoleDto
        {
            Id = roleToDelete.Id,
            DeletedBy = currentUserId,
            DeletedOn = now,
        };
    }

    private async Task AddPermissionsAsync(
        Role role,
        IEnumerable<string> permissionIds,
        CancellationToken cancellationToken
    )
    {
        foreach (var permissionId in permissionIds)
        {
            if (!await DB.Permissions.AnyAsync(p => p.Id == permissionId, cancellationToken))
                throw new InvalidOperationException($"Permission '{permissionId}' does not exist.");

            role.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });
        }
    }

    private static RoleDto MapToDto(Role role) =>
        new()
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            ConcurrencyToken = role.ConcurrencyToken,
            Permissions = role.RolePermissions.Select(rp => rp.Permission.Adapt<PermissionDto>()).ToList(),
        };
}
