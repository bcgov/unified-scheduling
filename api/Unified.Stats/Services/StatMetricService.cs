using Mapster;
using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Stats.Models;

namespace Unified.Stats.Services;

public sealed class StatMetricService(UnifiedDbContext db) : IStatMetricService
{
    public async Task<IReadOnlyCollection<StatMetricResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.StatMetrics
            .AsNoTracking()
            .OrderBy(m => m.Name)
            .ProjectToType<StatMetricResponse>()
            .ToListAsync(cancellationToken);
    }

    public async Task<StatMetricResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await db.StatMetrics
            .AsNoTracking()
            .Where(m => m.Id == id)
            .ProjectToType<StatMetricResponse>()
            .SingleOrDefaultAsync(cancellationToken);
    }
}
