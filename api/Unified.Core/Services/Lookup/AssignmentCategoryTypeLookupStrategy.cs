using Microsoft.EntityFrameworkCore;
using Unified.Core.Models;
using Unified.Db;

namespace Unified.Core.Services.Lookup;

public sealed class AssignmentCategoryTypeLookupStrategy(UnifiedDbContext db) : ILookupStrategy
{
    public LookupCodeTypes CodeType => LookupCodeTypes.AssignmentCategoryTypes;

    public async Task<IReadOnlyCollection<LookupCodeResponse>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await db
            .AssignmentCategoryTypes.AsNoTracking()
            .OrderBy(x => x.Description)
            .Select(x => new LookupCodeResponse
            {
                Code = x.Code,
                Description = x.Description,
                EffectiveDate = x.EffectiveDate,
                ExpiryDate = x.ExpiryDate,
                ChildCodeTypeIds = x.ChildCodeTypes.OrderBy(child => child.Id).Select(child => child.Id).ToList(),
            })
            .ToListAsync(cancellationToken);
    }
}
