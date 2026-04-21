using Mapster;
using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models.Stats;
using Unified.Stats.Models;

namespace Unified.Stats.Services;

public sealed class StatSignoffService(UnifiedDbContext db) : IStatSignoffService
{
    public async Task<IReadOnlyCollection<StatSignoffResponse>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await db
            .StatSignoffs.AsNoTracking()
            .OrderByDescending(s => s.Year)
            .ThenByDescending(s => s.Month)
            .ProjectToType<StatSignoffResponse>()
            .ToListAsync(cancellationToken);
    }

    public async Task<StatSignoffResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await db
            .StatSignoffs.AsNoTracking()
            .Where(s => s.Id == id)
            .ProjectToType<StatSignoffResponse>()
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<StatSignoffResponse> CreateAsync(
        StatSignoffRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var entity = new StatSignoff
        {
            UserId = request.UserId,
            LocationId = request.LocationId,
            Month = request.Month,
            Year = request.Year,
            SignoffDate = request.SignoffDate,
        };

        db.StatSignoffs.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return entity.Adapt<StatSignoffResponse>();
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.StatSignoffs.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity is null)
            return false;

        db.StatSignoffs.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
