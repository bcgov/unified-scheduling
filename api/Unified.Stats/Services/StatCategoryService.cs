using Mapster;
using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Stats.Models;

namespace Unified.Stats.Services;

public sealed class StatCategoryService(UnifiedDbContext db) : IStatCategoryService
{
    public async Task<IReadOnlyCollection<StatCategoryResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.StatCategories
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ProjectToType<StatCategoryResponse>()
            .ToListAsync(cancellationToken);
    }

    public async Task<StatCategoryResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await db.StatCategories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .ProjectToType<StatCategoryResponse>()
            .SingleOrDefaultAsync(cancellationToken);
    }
}
