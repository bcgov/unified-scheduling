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
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Code = "Superintendent",
            Description = "Superintendent",
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Code = "Staff Inspector",
            Description = "Staff Inspector",
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Code = "Inspector",
            Description = "Inspector",
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Code = "Staff Sergeant",
            Description = "Staff Sergeant",
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Code = "Sergeant",
            Description = "Sergeant",
            EffectiveDate = SeedEffectiveDate,
        },
        new()
        {
            Code = "Deputy Sheriff",
            Description = "Deputy Sheriff",
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
