using FluentValidation;
using Mapster;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Authorization.Claims;
using Unified.Common.Helpers.Extensions;
using Unified.Db;
using Unified.Db.Extensions;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement.Models;
using Unified.UserManagement.Validators;

namespace Unified.UserManagement.Services;

public sealed class RoleService(
    UnifiedDbContext DB,
    IHttpContextAccessor httpContextAccessor,
    DeleteRoleWithReassignmentRequestDtoValidator deleteRoleValidator,
    ILogger<RoleService> logger
) : IRoleService
{
    public async Task<IReadOnlyCollection<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Retrieving roles");

        var roles = await DB
            .Roles.AsNoTracking()
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
        logger.LogDebug("Retrieving active users assigned to role {RoleId}", roleId);

        var roleExists = await DB.Roles.WhereActive().AnyAsync(r => r.Id == roleId, cancellationToken);
        if (!roleExists)
            throw new KeyNotFoundException($"Role {roleId} not found.");

        var now = DateTimeOffset.UtcNow;

        return await DB
            .UserRoles.AsNoTracking()
            .Where(ur => ur.RoleId == roleId && (ur.ExpiryDate == null || ur.ExpiryDate > now))
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
        logger.LogInformation("Creating role with {PermissionCount} permissions", request.PermissionIds.Count);

        if (await DB.Roles.WhereActive().AnyAsync(r => r.Name.ToLower() == request.Name.ToLower(), cancellationToken))
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

        logger.LogInformation("Created role {RoleId}", role.Id);

        return MapToDto(role);
    }

    public async Task<RoleDto> UpdateAsync(UpdateRoleRequestDto request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Updating role {RoleId} with {PermissionCount} permissions",
            request.Id,
            request.PermissionIds.Count
        );

        var role =
            await DB
                .Roles.Include(r => r.RolePermissions)
                .WhereActive()
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Role {request.Id} not found.");

        if (role.ConcurrencyToken != request.ConcurrencyToken)
            throw new InvalidOperationException("The role was modified by another request. Please refresh and retry.");

        if (
            !string.Equals(role.Name, request.Name, StringComparison.OrdinalIgnoreCase)
            && await DB
                .Roles.WhereActive()
                .AnyAsync(r => r.Id != request.Id && r.Name.ToLower() == request.Name.ToLower(), cancellationToken)
        )
            throw new InvalidOperationException($"A role with name '{request.Name}' already exists.");

        role.Name = request.Name;
        role.Description = request.Description;

        var requestedIds = request.PermissionIds.ToHashSet();
        var existingIds = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();
        var toAdd = requestedIds.Except(existingIds).ToList();
        var toRemove = role.RolePermissions.Where(rp => !requestedIds.Contains(rp.PermissionId)).ToList();

        logger.LogDebug(
            "UpdateRole {RoleId}: existingIds=[{ExistingIds}] requestedIds=[{RequestedIds}] toAdd=[{ToAdd}] toRemove=[{ToRemove}]",
            role.Id,
            string.Join(", ", existingIds),
            string.Join(", ", requestedIds),
            string.Join(", ", toAdd),
            string.Join(", ", toRemove.Select(rp => rp.PermissionId))
        );

        foreach (var rp in toRemove)
            role.RolePermissions.Remove(rp);

        logger.LogDebug(
            "Role {RoleId} permission changes: adding {AddedCount}, removing {RemovedCount}",
            role.Id,
            toAdd.Count,
            existingIds.Except(requestedIds).Count()
        );
        await AddPermissionsAsync(role, toAdd, cancellationToken);
        await DB.SaveChangesAsync(cancellationToken);

        await DB.Entry(role)
            .Collection(r => r.RolePermissions)
            .Query()
            .Include(rp => rp.Permission)
            .LoadAsync(cancellationToken);

        logger.LogInformation("Updated role {RoleId}", role.Id);

        return MapToDto(role);
    }

    public async Task<DeletedRoleDto> ReassignAndDeleteAsync(
        int roleIdToDelete,
        DeleteRoleWithReassignmentRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        var currentUserId = httpContextAccessor.HttpContext!.User.CurrentUserId();
        var now = DateTimeOffset.UtcNow;

        logger.LogInformation("Deleting role {RoleId}", roleIdToDelete);

        var roleToDelete =
            await DB.Roles.WhereActive().FirstOrDefaultAsync(r => r.Id == roleIdToDelete, cancellationToken)
            ?? throw new KeyNotFoundException($"Role {roleIdToDelete} not found.");
        var activeAssignments = await DB
            .UserRoles.Where(ur => ur.RoleId == roleIdToDelete && (ur.ExpiryDate == null || ur.ExpiryDate > now))
            .ToListAsync(cancellationToken);

        var activeAssignmentCount = activeAssignments.Count;

        if (activeAssignmentCount > 0)
        {
            logger.LogInformation(
                "Reassigning {AssignmentCount} active assignments from role {OldRoleId} to role {NewRoleId}",
                activeAssignmentCount,
                roleIdToDelete,
                request.NewRoleId
            );

            await deleteRoleValidator.ValidateAndThrowAsync(request, cancellationToken);

            if (request.NewRoleId == roleIdToDelete)
                throw new InvalidOperationException("The new role cannot be the same as the role being deleted.");

            var newRoleExists = await DB
                .Roles.WhereActive()
                .AnyAsync(r => r.Id == request.NewRoleId, cancellationToken);
            if (!newRoleExists)
                throw new KeyNotFoundException($"New role {request.NewRoleId} not found.");

            var timezoneId = httpContextAccessor.HttpContext!.User.HomeLocationTimezone();
            var effectiveDate = DateTimeOffsetExtensions.FromDateStringToStartOfDayInTimeZone(
                request.NewRoleEffectiveDate!,
                timezoneId
            );

            DateTimeOffset? expiryDate = null;
            if (!string.IsNullOrWhiteSpace(request.NewRoleExpiryDate))
            {
                expiryDate = DateTimeOffsetExtensions.FromDateStringToEndOfDayInTimeZone(
                    request.NewRoleExpiryDate,
                    timezoneId
                );
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
                    logger.LogDebug(
                        "Updating existing role assignment for user {UserId} and role {RoleId}",
                        assignment.UserId,
                        request.NewRoleId
                    );

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
                        RoleId = request.NewRoleId!.Value,
                        EffectiveDate = effectiveDate,
                        ExpiryDate = expiryDate,
                        ExpiryReason = null,
                    }
                );

                logger.LogDebug(
                    "Creating replacement role assignment for user {UserId} and role {RoleId}",
                    assignment.UserId,
                    request.NewRoleId
                );
            }

            DB.UserRoles.RemoveRange(activeAssignments);

            roleToDelete.DeletedOn = now;
            roleToDelete.DeletedById = currentUserId;
            await DB.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Deleted role {RoleId} after reassigning {AssignmentCount} active assignments",
                roleToDelete.Id,
                activeAssignmentCount
            );

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

        logger.LogInformation("Deleted role {RoleId} with no active assignments", roleToDelete.Id);

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
            DeletedOn = role.DeletedOn,
            DeletedById = role.DeletedById,
            Permissions = role.RolePermissions.Select(rp => rp.Permission.Adapt<PermissionDto>()).ToList(),
        };
}
