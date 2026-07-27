using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models;

namespace Unified.UserManagement.Seeders;

/// <summary>
/// Aggregates configured region data sets and upserts them into the Region table.
/// </summary>
public sealed class RegionSeeder(ILogger<RegionSeeder> logger, IEnumerable<RegionSeedConfiguration> configurations)
    : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 1;

    public override string Name => "Region";

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        ValidateDefinitions(configurations);
        var seedRegions = configurations.SelectMany(configuration => configuration.Definitions).ToArray();

        var createdCount = 0;
        var updatedCount = 0;

        foreach (var seedRegion in seedRegions)
        {
            var existingRegion = await dbContext.Regions.FirstOrDefaultAsync(
                region => region.Id == seedRegion.Id,
                cancellationToken
            );

            if (existingRegion is null)
            {
                Logger.LogInformation("Region with Id {Id} does not exist, adding it...", seedRegion.Id);
                await dbContext.Regions.AddAsync(
                    new Region
                    {
                        Id = seedRegion.Id,
                        JustinId = seedRegion.JustinId,
                        Code = seedRegion.Code,
                        Name = seedRegion.Name,
                    },
                    cancellationToken
                );
                createdCount++;
                continue;
            }

            Logger.LogInformation("Updating fields for region with Id {Id}...", seedRegion.Id);
            existingRegion.Code = seedRegion.Code;
            existingRegion.Name = seedRegion.Name;
            updatedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        Logger.LogInformation(
            "Region seeding complete. Created {CreatedCount}, updated {UpdatedCount}.",
            createdCount,
            updatedCount
        );
    }

    private static void ValidateDefinitions(IEnumerable<RegionSeedConfiguration> configurations)
    {
        var definitions = configurations
            .SelectMany(configuration =>
                configuration.Definitions.Select(region => (Definition: region, configuration.Source))
            )
            .ToArray();
        SeedDefinitionValidator.ThrowIfDuplicateValues(
            definitions,
            "region",
            (region => region.Id.ToString(), "Id", StringComparer.Ordinal),
            (region => region.Code, "Code", StringComparer.OrdinalIgnoreCase)
        );
    }
}
