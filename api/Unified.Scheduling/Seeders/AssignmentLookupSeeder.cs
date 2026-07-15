using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Lookup;

namespace Unified.Scheduling.Seeders;

public sealed class AssignmentLookupSeeder(ILogger<AssignmentLookupSeeder> logger)
    : SeederBase<UnifiedDbContext>(logger)
{
    private static readonly DateTimeOffset SeedEffectiveDate = new(2020, 6, 10, 0, 0, 0, TimeSpan.Zero);

    private static readonly (string Code, string Description)[] CategoryTypes =
    [
        ("CourtRoom", "Court Room"),
        ("CourtRole", "Court Assignment"),
        ("JailRole", "Jail Assignment"),
        ("EscortRun", "Transport Assignment"),
        ("OtherAssignment", "Other Assignment"),
    ];

    private static readonly (string Code, string Description, string ParentCategoryCode)[] SubCategoryTypes =
    [
        ("PROVINCIAL", "Provincial", "CourtRoom"),
        ("SUPREME", "Supreme", "CourtRoom"),
        ("IN_CUSTODY", "In custody", "EscortRun"),
        ("OUT_OF_CUSTODY", "Out of custody", "EscortRun"),
        ("OTHER", "Other", "OtherAssignment"),
    ];

    public override int Order => 12;

    public override string Name => "SchedulingAssignmentLookups";

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        var categoryTypesByCode = await UpsertCodesAsync(dbContext.AssignmentCategoryTypes, CategoryTypes, cancellationToken);
        await UpsertSubCategoryCodesAsync(dbContext, categoryTypesByCode, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Dictionary<string, T>> UpsertCodesAsync<T>(
        DbSet<T> set,
        IReadOnlyCollection<(string Code, string Description)> values,
        CancellationToken cancellationToken
    )
        where T : BaseCodeTypeEntity, new()
    {
        var entitiesByCode = new Dictionary<string, T>(StringComparer.Ordinal);

        foreach (var (code, description) in values)
        {
            var existing = await set.SingleOrDefaultAsync(item => item.Code == code, cancellationToken);
            if (existing is null)
            {
                existing = new T
                {
                    Code = code,
                    Description = description,
                    EffectiveDate = SeedEffectiveDate,
                };
                await set.AddAsync(existing, cancellationToken);
                entitiesByCode[code] = existing;
                continue;
            }

            existing.Description = description;
            existing.EffectiveDate = SeedEffectiveDate;
            existing.ExpiryDate = null;
            entitiesByCode[code] = existing;
        }

        return entitiesByCode;
    }

    private static async Task UpsertSubCategoryCodesAsync(
        UnifiedDbContext dbContext,
        IReadOnlyDictionary<string, AssignmentCategoryType> categoryTypesByCode,
        CancellationToken cancellationToken
    )
    {
        foreach (var (code, description, parentCategoryCode) in SubCategoryTypes)
        {
            var parentCategoryType = categoryTypesByCode[parentCategoryCode];
            var existing = await dbContext
                .AssignmentSubCategoryTypes.SingleOrDefaultAsync(item => item.Code == code, cancellationToken);

            if (existing is null)
            {
                existing = new AssignmentSubCategoryType
                {
                    Code = code,
                    Description = description,
                    EffectiveDate = SeedEffectiveDate,
                    ParentCodeType = parentCategoryType,
                    ParentCodeTypeId = parentCategoryType.Id,
                };

                await dbContext.AssignmentSubCategoryTypes.AddAsync(existing, cancellationToken);
                continue;
            }

            existing.Description = description;
            existing.EffectiveDate = SeedEffectiveDate;
            existing.ExpiryDate = null;
            existing.ParentCodeType = parentCategoryType;
            existing.ParentCodeTypeId = parentCategoryType.Id;
        }
    }
}
