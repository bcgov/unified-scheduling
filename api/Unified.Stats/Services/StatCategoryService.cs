using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Db;
using Unified.Stats.Models;

namespace Unified.Stats.Services;

public sealed class StatCategoryService(UnifiedDbContext db, ILogger<StatCategoryService> logger) : IStatCategoryService
{
    public async Task<IReadOnlyCollection<StatCategoryResponse>> GetAllAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Retrieving stat categories");

        var query = db.StatCategories.AsNoTracking();
        if (!includeArchived)
            query = query.Where(c => !c.IsArchived);

        return await query
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ProjectToType<StatCategoryResponse>()
            .ToListAsync(cancellationToken);
    }

    public async Task<StatCategoryResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Retrieving stat category {StatCategoryId}", id);

        return await db
            .StatCategories.AsNoTracking()
            .Where(c => c.Id == id)
            .ProjectToType<StatCategoryResponse>()
            .SingleOrDefaultAsync(cancellationToken);
    }
}
