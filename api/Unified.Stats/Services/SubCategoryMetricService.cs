using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Db;
using Unified.Stats.Models;

namespace Unified.Stats.Services;

public sealed class SubCategoryMetricService(UnifiedDbContext db, ILogger<SubCategoryMetricService> logger)
    : ISubCategoryMetricService
{
    public async Task<IReadOnlyCollection<SubCategoryMetricResponse>> GetAllAsync(
        int? subCategoryId = null,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Retrieving sub-category metrics for sub-category {SubCategoryId}", subCategoryId);

        var query = db.SubCategoryMetrics.AsNoTracking();

        if (subCategoryId is int id)
            query = query.Where(scm => scm.SubCategoryId == id);

        return await query
            .OrderBy(scm => scm.DisplayOrder)
            .ProjectToType<SubCategoryMetricResponse>()
            .ToListAsync(cancellationToken);
    }

    public async Task<SubCategoryMetricResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Retrieving sub-category metric {SubCategoryMetricId}", id);

        return await db
            .SubCategoryMetrics.AsNoTracking()
            .Where(scm => scm.Id == id)
            .ProjectToType<SubCategoryMetricResponse>()
            .SingleOrDefaultAsync(cancellationToken);
    }
}
