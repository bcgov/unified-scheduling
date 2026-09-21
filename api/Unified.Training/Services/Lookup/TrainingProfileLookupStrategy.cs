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

        return await query
            .OrderBy(profile => profile.Code)
            .Select(profile => new TrainingProfileLookupResponse
            {
                Id = profile.Id,
                Code = profile.Code,
                CreatedOn = profile.CreatedOn,
                UpdatedOn = profile.UpdatedOn,
            })
            .ToListAsync(cancellationToken);
    }
}
