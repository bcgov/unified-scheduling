using Microsoft.EntityFrameworkCore;
using Unified.Core.Models;
using Unified.Db;

namespace Unified.Core.Services.Lookup;

public sealed class AssignmentSubCategoryTypeLookupStrategy(UnifiedDbContext db) : ILookupStrategy
{
    public LookupCodeTypes CodeType => LookupCodeTypes.AssignmentSubCategoryTypes;

    public async Task<IReadOnlyCollection<LookupCodeResponse>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await db
            .AssignmentSubCategoryTypes.AsNoTracking()
            .OrderBy(x => x.Description)
            .Select(x => new LookupCodeResponse
            {
                Code = x.Code,
                Description = x.Description,
                EffectiveDate = x.EffectiveDate,
                ExpiryDate = x.ExpiryDate,
                ParentCodeTypeId = x.ParentCodeTypeId,
            })
            .ToListAsync(cancellationToken);
    }
}
