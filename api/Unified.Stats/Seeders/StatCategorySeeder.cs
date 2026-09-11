using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Stats;

namespace Unified.Stats.Seeders;

public class StatCategorySeeder(ILogger<StatCategorySeeder> logger) : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 11;

    public override string Name => "StatCategory";

    private static readonly StatCategory[] SeedCategories =
    [
        // Non-Supervision (GroupId = 1) — alphabetical
        new()
        {
            Id = 2,
            GroupId = 1,
            Name = "Circuit court related travel",
            DisplayOrder = 1,
        },
        new()
        {
            Id = 3,
            GroupId = 1,
            Name = "Coroner Jury Administration",
            DisplayOrder = 2,
        },
        new()
        {
            Id = 1,
            GroupId = 1,
            Name = "Court Security",
            DisplayOrder = 3,
        },
        new()
        {
            Id = 4,
            GroupId = 1,
            Name = "Criminal/Civil Jury Administration",
            DisplayOrder = 4,
        },
        new()
        {
            Id = 5,
            GroupId = 1,
            Name = "Documents Civil/Family",
            DisplayOrder = 5,
        },
        new()
        {
            Id = 6,
            GroupId = 1,
            Name = "Documents Criminal",
            DisplayOrder = 6,
        },
        new()
        {
            Id = 11,
            GroupId = 1,
            Name = "Holding area/cellblock",
            DisplayOrder = 7,
        },
        new()
        {
            Id = 12,
            GroupId = 1,
            Name = "Other",
            DisplayOrder = 8,
        },
        new()
        {
            Id = 13,
            GroupId = 1,
            Name = "PIO/SIO",
            DisplayOrder = 9,
        },
        new()
        {
            Id = 14,
            GroupId = 1,
            Name = "Training",
            DisplayOrder = 10,
        },
        new()
        {
            Id = 7,
            GroupId = 1,
            Name = "Transports Air",
            DisplayOrder = 11,
        },
        new()
        {
            Id = 9,
            GroupId = 1,
            Name = "Transports Females",
            IsArchived = true,
            DisplayOrder = 12,
        },
        new()
        {
            Id = 8,
            GroupId = 1,
            Name = "Transports Ground",
            DisplayOrder = 13,
        },
        new()
        {
            Id = 10,
            GroupId = 1,
            Name = "Transports Males",
            IsArchived = true,
            DisplayOrder = 14,
        },
        // Supervision (GroupId = 2) — alphabetical
        new()
        {
            Id = 16,
            GroupId = 2,
            Name = "Circuit court related travel",
            DisplayOrder = 1,
        },
        new()
        {
            Id = 34,
            GroupId = 2,
            Name = "Coroner Jury Administration",
            DisplayOrder = 2,
        },
        new()
        {
            Id = 15,
            GroupId = 2,
            Name = "Court Security",
            DisplayOrder = 3,
        },
        new()
        {
            Id = 17,
            GroupId = 2,
            Name = "Documents Civil/Family",
            DisplayOrder = 4,
        },
        new()
        {
            Id = 18,
            GroupId = 2,
            Name = "Documents Criminal",
            DisplayOrder = 5,
        },
        new()
        {
            Id = 21,
            GroupId = 2,
            Name = "Holding area/cellblock",
            DisplayOrder = 6,
        },
        new()
        {
            Id = 22,
            GroupId = 2,
            Name = "Jury Administration",
            DisplayOrder = 7,
        },
        new()
        {
            Id = 23,
            GroupId = 2,
            Name = "Other",
            DisplayOrder = 8,
        },
        new()
        {
            Id = 24,
            GroupId = 2,
            Name = "PIO/SIO",
            DisplayOrder = 9,
        },
        new()
        {
            Id = 25,
            GroupId = 2,
            Name = "Training",
            DisplayOrder = 10,
        },
        new()
        {
            Id = 19,
            GroupId = 2,
            Name = "Transports Air",
            DisplayOrder = 11,
        },
        new()
        {
            Id = 20,
            GroupId = 2,
            Name = "Transports Ground",
            DisplayOrder = 12,
        },
        // Location Level (GroupId = 3) — alphabetical
        new()
        {
            Id = 26,
            GroupId = 3,
            Name = "Circuit court related travel",
            DisplayOrder = 1,
        },
        new()
        {
            Id = 30,
            GroupId = 3,
            Name = "Coroner Jury Administration",
            DisplayOrder = 2,
        },
        new()
        {
            Id = 31,
            GroupId = 3,
            Name = "Criminal/Civil Jury Administration",
            DisplayOrder = 3,
        },
        new()
        {
            Id = 35,
            GroupId = 3,
            Name = "Documents Civil/Family",
            DisplayOrder = 4,
        },
        new()
        {
            Id = 36,
            GroupId = 3,
            Name = "Documents Criminal",
            DisplayOrder = 5,
        },
        new()
        {
            Id = 29,
            GroupId = 3,
            Name = "Holding area/cellblock",
            DisplayOrder = 6,
        },
        new()
        {
            Id = 27,
            GroupId = 3,
            Name = "Transports Air",
            DisplayOrder = 7,
        },
        new()
        {
            Id = 32,
            GroupId = 3,
            Name = "Transports Females",
            DisplayOrder = 8,
        },
        new()
        {
            Id = 28,
            GroupId = 3,
            Name = "Transports Ground",
            DisplayOrder = 9,
        },
        new()
        {
            Id = 33,
            GroupId = 3,
            Name = "Transports Males",
            DisplayOrder = 10,
        },
    ];

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Updating stat categories...");

        var createdCount = 0;
        var updatedCount = 0;

        foreach (var seed in SeedCategories)
        {
            var existing = await dbContext.StatCategories.FirstOrDefaultAsync(c => c.Id == seed.Id, cancellationToken);

            if (existing is null)
            {
                Logger.LogInformation("StatCategory with Id {Id} does not exist, adding it...", seed.Id);
                await dbContext.StatCategories.AddAsync(seed, cancellationToken);
                createdCount++;
                continue;
            }

            Logger.LogInformation("Updating fields for StatCategory with Id {Id}...", seed.Id);
            existing.GroupId = seed.GroupId;
            existing.Name = seed.Name;
            existing.DisplayOrder = seed.DisplayOrder;
            updatedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        Logger.LogInformation(
            "StatCategory seeding complete. Created {CreatedCount}, updated {UpdatedCount}.",
            createdCount,
            updatedCount
        );
    }
}
