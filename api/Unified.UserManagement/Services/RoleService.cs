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
        return await DB
            .Roles.AsNoTracking()
            .OrderBy(x => x.Name)
            .ProjectToType<RoleDto>()
            .ToListAsync(cancellationToken);
    }

    public async Task<RoleDto> CreateAsync(RoleRequestDto request, CancellationToken cancellationToken = default)
    {
        var role = new Role
        {
            Name = request.Name,
            Description = request.Description,
        };

        DB.Roles.Add(role);
        await DB.SaveChangesAsync(cancellationToken);

        return role.Adapt<RoleDto>();
    }
}
