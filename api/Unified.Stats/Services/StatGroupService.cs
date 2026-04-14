using Mapster;
using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Stats.Models;

namespace Unified.Stats.Services;

public sealed class StatGroupService(UnifiedDbContext db) : IStatGroupService
{
    public async Task<IReadOnlyCollection<StatGroupResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.StatGroups
            .AsNoTracking()
            .OrderBy(g => g.DisplayOrder)
            .ThenBy(g => g.Name)
            .ProjectToType<StatGroupResponse>()
            .ToListAsync(cancellationToken);
    }

    public async Task<StatGroupResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await db.StatGroups
            .AsNoTracking()
            .Where(g => g.Id == id)
            .ProjectToType<StatGroupResponse>()
            .SingleOrDefaultAsync(cancellationToken);
    }
}
