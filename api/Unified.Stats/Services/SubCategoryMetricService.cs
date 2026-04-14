using Mapster;
using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Stats.Models;

namespace Unified.Stats.Services;

public sealed class SubCategoryMetricService(UnifiedDbContext db) : ISubCategoryMetricService
{
    public async Task<IReadOnlyCollection<SubCategoryMetricResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.SubCategoryMetrics
            .AsNoTracking()
            .OrderBy(scm => scm.DisplayOrder)
            .ProjectToType<SubCategoryMetricResponse>()
            .ToListAsync(cancellationToken);
    }

    public async Task<SubCategoryMetricResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await db.SubCategoryMetrics
            .AsNoTracking()
            .Where(scm => scm.Id == id)
            .ProjectToType<SubCategoryMetricResponse>()
            .SingleOrDefaultAsync(cancellationToken);
    }
}
