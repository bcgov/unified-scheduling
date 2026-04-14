using Mapster;
using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Stats.Models;

namespace Unified.Stats.Services;

public sealed class SubCategoryService(UnifiedDbContext db) : ISubCategoryService
{
    public async Task<IReadOnlyCollection<SubCategoryResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.SubCategories
            .AsNoTracking()
            .OrderBy(sc => sc.DisplayOrder)
            .ThenBy(sc => sc.Name)
            .ProjectToType<SubCategoryResponse>()
            .ToListAsync(cancellationToken);
    }

    public async Task<SubCategoryResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await db.SubCategories
            .AsNoTracking()
            .Where(sc => sc.Id == id)
            .ProjectToType<SubCategoryResponse>()
            .SingleOrDefaultAsync(cancellationToken);
    }
}
