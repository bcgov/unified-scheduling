using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models;

namespace Unified.Core.Seeders;

/// <summary>
/// Seeder for the Region table.
/// </summary>
public class RegionSeeder(ILogger<RegionSeeder> logger) : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 1;

    public override string Name => "Region";

    private static readonly Region[] SeedRegions =
    [
        new() { Id = 100, Name = "Central Programs", Code = "CP" },
        new() { Id = 101, Name = "Office of the Chief Sheriff", Code = "OCS" },
    ];

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Updating regions...");

        var createdCount = 0;
        var updatedCount = 0;

        foreach (var seedRegion in SeedRegions)
        {
            var existingRegion = await dbContext
                .Regions.AsQueryable()
                .FirstOrDefaultAsync(r => r.Id == seedRegion.Id, cancellationToken);

            if (existingRegion is null)
            {
                Logger.LogInformation("Region with Id {Id} does not exist, adding it...", seedRegion.Id);
                await dbContext.Regions.AddAsync(seedRegion, cancellationToken);
                createdCount++;
                continue;
            }

            Logger.LogInformation("Updating fields for region with Id {Id}...", seedRegion.Id);
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
}
