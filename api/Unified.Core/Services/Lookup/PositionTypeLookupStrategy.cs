using Microsoft.EntityFrameworkCore;
using Unified.Core.Models;
using Unified.Db;

namespace Unified.Core.Services.Lookup;

public sealed class PositionTypeLookupStrategy(UnifiedDbContext db) : ILookupStrategy
{
    public LookupCodeTypes CodeType => LookupCodeTypes.PositionTypes;

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<LookupCodeResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db
            .PositionTypes.AsNoTracking()
            .OrderBy(x => x.Description)
            .Select(x => new LookupCodeResponse
            {
                Code = x.Code,
                Description = x.Description,
                EffectiveDate = x.EffectiveDate,
                ExpiryDate = x.ExpiryDate,
            })
            .ToListAsync(cancellationToken);
    }
}
