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

    private static readonly (string Code, string Description)[] SubCategoryTypes =
    [
        ("PROVINCIAL", "Provincial"),
        ("SUPREME", "Supreme"),
        ("IN_CUSTODY", "In custody"),
        ("OUT_OF_CUSTODY", "Out of custody"),
        ("OTHER", "Other"),
    ];

    public override int Order => 12;

    public override string Name => "SchedulingAssignmentLookups";

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        await UpsertCodesAsync(dbContext.AssignmentCategoryTypes, CategoryTypes, cancellationToken);
        await UpsertCodesAsync(dbContext.AssignmentSubCategoryTypes, SubCategoryTypes, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpsertCodesAsync<T>(
        DbSet<T> set,
        IReadOnlyCollection<(string Code, string Description)> values,
        CancellationToken cancellationToken
    )
        where T : BaseCodeTypeEntity, new()
    {
        foreach (var (code, description) in values)
        {
            var existing = await set.SingleOrDefaultAsync(item => item.Code == code, cancellationToken);
            if (existing is null)
            {
                await set.AddAsync(
                    new T
                    {
                        Code = code,
                        Description = description,
                        EffectiveDate = SeedEffectiveDate,
                    },
                    cancellationToken
                );
                continue;
            }

            existing.Description = description;
            existing.EffectiveDate = SeedEffectiveDate;
            existing.ExpiryDate = null;
        }
    }
}
