using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Db;
using Unified.Stats.Models;

namespace Unified.Stats.Services;

public sealed class StatCategoryService(UnifiedDbContext db, ILogger<StatCategoryService> logger) : IStatCategoryService
{
    public async Task<IReadOnlyCollection<StatCategoryResponse>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Retrieving stat categories");

        return await db
            .StatCategories.AsNoTracking()
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
