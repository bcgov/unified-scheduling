using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Db;
using Unified.Stats.Models;

namespace Unified.Stats.Services;

public sealed class StatGroupService(UnifiedDbContext db, ILogger<StatGroupService> logger) : IStatGroupService
{
    public async Task<IReadOnlyCollection<StatGroupResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Retrieving stat groups");

        return await db
            .StatGroups.AsNoTracking()
            .OrderBy(g => g.DisplayOrder)
            .ThenBy(g => g.Name)
            .ProjectToType<StatGroupResponse>()
            .ToListAsync(cancellationToken);
    }

    public async Task<StatGroupResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Retrieving stat group {StatGroupId}", id);

        return await db
            .StatGroups.AsNoTracking()
            .Where(g => g.Id == id)
            .ProjectToType<StatGroupResponse>()
            .SingleOrDefaultAsync(cancellationToken);
    }
}
