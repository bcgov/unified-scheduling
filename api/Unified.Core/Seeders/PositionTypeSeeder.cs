using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Lookup;

namespace Unified.Core.Seeders;

/// <summary>
/// Seeder for the PositionType table.
/// </summary>
public class PositionTypeSeeder(ILogger<PositionTypeSeeder> logger) : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 1;

    public override string Name => "PositionType";

    private static readonly DateTimeOffset SeedEffectiveDate = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly PositionType[] SeedPositionTypes =
    [
        new()
        {
            Code = "Chief Sheriff",
            Description = "Chief Sheriff",
            DisplayOrder = 1,
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Code = "Deputy Chief Sheriff",
            Description = "Deputy Chief Sheriff",
            DisplayOrder = 2,
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Code = "Superintendent",
            Description = "Superintendent",
            DisplayOrder = 3,
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Code = "Senior Inspector",
            Description = "Senior Inspector",
            DisplayOrder = 4,
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Code = "Staff Inspector",
            Description = "Staff Inspector",
            DisplayOrder = 5,
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Code = "Inspector",
            Description = "Inspector",
            DisplayOrder = 6,
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Code = "Staff Sergeant",
            Description = "Staff Sergeant",
            DisplayOrder = 7,
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Code = "Sergeant",
            Description = "Sergeant",
            DisplayOrder = 8,
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Code = "Deputy Sheriff",
            Description = "Deputy Sheriff",
            DisplayOrder = 9,
            EffectiveDate = SeedEffectiveDate,
        },
    ];

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Updating position types...");

        var createdCount = 0;
        var updatedCount = 0;

        foreach (var seedPositionType in SeedPositionTypes)
        {
            var existingPositionType = await dbContext
                .PositionTypes.AsQueryable()
                .FirstOrDefaultAsync(positionType => positionType.Code == seedPositionType.Code, cancellationToken);

            if (existingPositionType is null)
            {
                Logger.LogInformation(
                    "Position type with code {Code} does not exist, adding it...",
                    seedPositionType.Code
                );
                await dbContext.PositionTypes.AddAsync(seedPositionType, cancellationToken);
                createdCount++;
                continue;
            }

            Logger.LogInformation("Updating fields for position type with code {Code}...", seedPositionType.Code);
            existingPositionType.Description = seedPositionType.Description;
            existingPositionType.DisplayOrder = seedPositionType.DisplayOrder;
            existingPositionType.EffectiveDate = seedPositionType.EffectiveDate;
            existingPositionType.ExpiryDate = seedPositionType.ExpiryDate;

            updatedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        Logger.LogInformation(
            "Position type seeding complete. Created {CreatedCount}, updated {UpdatedCount}.",
            createdCount,
            updatedCount
        );
    }
}
