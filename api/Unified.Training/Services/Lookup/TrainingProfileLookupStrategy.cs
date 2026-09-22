using Microsoft.EntityFrameworkCore;
using Unified.Core.Models;
using Unified.Db;

namespace Unified.Training.Services.Lookup;

public sealed class TrainingProfileLookupStrategy(UnifiedDbContext db) : ITrainingProfileLookupStrategy
{
    public LookupCodeTypes CodeType => LookupCodeTypes.TrainingProfiles;

    public async Task<IReadOnlyCollection<LookupCodeResponse>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await db
            .TrainingProfileTypes.AsNoTracking()
            .OrderBy(profile => profile.Name)
            .ThenBy(profile => profile.Code)
            .Select(profile => new LookupCodeResponse
            {
                Code = profile.Code,
                Description = profile.Name,
                EffectiveDate = profile.CreatedOn,
                ExpiryDate = null,
            })
            .ToListAsync(cancellationToken);
    }
}
