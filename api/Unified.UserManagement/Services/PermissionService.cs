using Mapster;
using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Services;

public sealed class PermissionService(UnifiedDbContext DB) : IPermissionService
{
    public async Task<IReadOnlyCollection<PermissionDto>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await DB
            .Permissions.AsNoTracking()
            .OrderBy(x => x.Id)
            .ProjectToType<PermissionDto>()
            .ToListAsync(cancellationToken);
    }
}
