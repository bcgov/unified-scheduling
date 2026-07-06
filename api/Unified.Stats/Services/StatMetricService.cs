using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Db;
using Unified.Stats.Models;

namespace Unified.Stats.Services;

public sealed class StatMetricService(UnifiedDbContext db, ILogger<StatMetricService> logger) : IStatMetricService
{
    public async Task<IReadOnlyCollection<StatMetricResponse>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Retrieving stat metrics");

        return await db
            .StatMetrics.AsNoTracking()
            .OrderBy(m => m.Name)
            .ProjectToType<StatMetricResponse>()
            .ToListAsync(cancellationToken);
    }

    public async Task<StatMetricResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Retrieving stat metric {StatMetricId}", id);

        return await db
            .StatMetrics.AsNoTracking()
            .Where(m => m.Id == id)
            .ProjectToType<StatMetricResponse>()
            .SingleOrDefaultAsync(cancellationToken);
    }
}
