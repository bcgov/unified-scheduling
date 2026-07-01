using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Db;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Services;

public sealed class PermissionService(UnifiedDbContext DB, ILogger<PermissionService> logger) : IPermissionService
{
    public async Task<IReadOnlyCollection<PermissionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Retrieving permissions");

        return await DB
            .Permissions.AsNoTracking()
            .OrderBy(x => x.Id)
            .ProjectToType<PermissionDto>()
            .ToListAsync(cancellationToken);
    }
}
