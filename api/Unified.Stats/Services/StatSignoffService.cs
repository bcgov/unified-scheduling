using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Db;
using Unified.Db.Models.Stats;
using Unified.Stats.Models;

namespace Unified.Stats.Services;

public sealed class StatSignoffService(UnifiedDbContext db, ILogger<StatSignoffService> logger) : IStatSignoffService
{
    public async Task<IReadOnlyCollection<StatSignoffResponse>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Retrieving stat signoffs");

        return await db
            .StatSignoffs.AsNoTracking()
            .OrderByDescending(s => s.Year)
            .ThenByDescending(s => s.Month)
            .ProjectToType<StatSignoffResponse>()
            .ToListAsync(cancellationToken);
    }

    public async Task<StatSignoffResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Retrieving stat signoff {StatSignoffId}", id);

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
        logger.LogInformation(
            "Creating stat signoff for user {UserId}, location {LocationId}, period {Year}-{Month}",
            request.UserId,
            request.LocationId,
            request.Year,
            request.Month
        );

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

        logger.LogInformation("Created stat signoff {StatSignoffId}", entity.Id);

        return entity.Adapt<StatSignoffResponse>();
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.StatSignoffs.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity is null)
        {
            logger.LogDebug("Stat signoff {StatSignoffId} was not found for delete", id);
            return false;
        }

        db.StatSignoffs.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted stat signoff {StatSignoffId}", id);

        return true;
    }
}
