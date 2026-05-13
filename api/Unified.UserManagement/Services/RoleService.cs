using Mapster;
using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Services;

public sealed class RoleService(UnifiedDbContext DB) : IRoleService
{
    public async Task<IReadOnlyCollection<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var roles = await DB
            .Roles.AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return roles.Select(MapToDto).ToList();
    }

    public async Task<RoleDto> CreateAsync(RoleRequestDto request, CancellationToken cancellationToken = default)
    {
        if (await DB.Roles.AnyAsync(r => r.Name.ToLower() == request.Name.ToLower(), cancellationToken))
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
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Role {request.Id} not found.");

        if (role.ConcurrencyToken != request.ConcurrencyToken)
            throw new InvalidOperationException("The role was modified by another request. Please refresh and retry.");

        if (
            !string.Equals(role.Name, request.Name, StringComparison.OrdinalIgnoreCase)
            && await DB.Roles.AnyAsync(
                r => r.Id != request.Id && r.Name.ToLower() == request.Name.ToLower(),
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

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var role = await DB.Roles.FindAsync([id], cancellationToken: cancellationToken)
            ?? throw new KeyNotFoundException($"Role {id} not found.");

        DB.Roles.Remove(role);
        await DB.SaveChangesAsync(cancellationToken);
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
