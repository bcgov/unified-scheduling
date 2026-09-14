using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Training.Models;

namespace Unified.Training.Services.Lookup;

public sealed class TrainingProfileLookupStrategy(UnifiedDbContext db) : ITrainingProfileLookupStrategy
{
    public async Task<IReadOnlyCollection<TrainingProfileLookupResponse>> GetAllAsync(
        bool includeExpired = false,
        CancellationToken cancellationToken = default
    )
    {
        var query = db.TrainingProfiles.AsNoTracking().AsQueryable();

        if (!includeExpired)
        {
            var now = DateTimeOffset.UtcNow;
            query = query.Where(profile => profile.ExpiryDate == null || profile.ExpiryDate > now);
        }

        return await query
            .OrderBy(profile => profile.Code)
            .Select(profile => new TrainingProfileLookupResponse
            {
                Id = profile.Id,
                Code = profile.Code,
                Description = profile.Description,
                EffectiveDate = profile.EffectiveDate,
                ExpiryDate = profile.ExpiryDate,
                CreatedOn = profile.CreatedOn,
                UpdatedOn = profile.UpdatedOn,
            })
            .ToListAsync(cancellationToken);
    }
}
