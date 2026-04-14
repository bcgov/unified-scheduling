using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Stats;

namespace Unified.Stats.Seeders;

public class StatGroupSeeder(ILogger<StatGroupSeeder> logger) : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 10;

    public override string Name => "StatGroup";

    private static readonly StatGroup[] SeedGroups =
    [
        new() { Id = 1, Name = "Non-Supervision", DisplayOrder = 1 },
        new() { Id = 2, Name = "Supervision", DisplayOrder = 2 },
    ];

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Updating stat groups...");

        var createdCount = 0;
        var updatedCount = 0;

        foreach (var seed in SeedGroups)
        {
            var existing = await dbContext.StatGroups
                .FirstOrDefaultAsync(g => g.Id == seed.Id, cancellationToken);

            if (existing is null)
            {
                Logger.LogInformation("StatGroup with Id {Id} does not exist, adding it...", seed.Id);
                await dbContext.StatGroups.AddAsync(seed, cancellationToken);
                createdCount++;
                continue;
            }

            Logger.LogInformation("Updating fields for StatGroup with Id {Id}...", seed.Id);
            existing.Name = seed.Name;
            existing.DisplayOrder = seed.DisplayOrder;
            updatedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        Logger.LogInformation(
            "StatGroup seeding complete. Created {CreatedCount}, updated {UpdatedCount}.",
            createdCount,
            updatedCount
        );
    }
}
