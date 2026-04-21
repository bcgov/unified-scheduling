using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Stats;

namespace Unified.Stats.Seeders;

public class StatMetricSeeder(ILogger<StatMetricSeeder> logger) : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 13;

    public override string Name => "StatMetric";

    private static readonly StatMetric[] SeedMetrics =
    [
        // Hours (IDs 1-23)
        new()
        {
            Id = 1,
            Name = "Staff Hours",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 2,
            Name = "Overtime Hours",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 3,
            Name = "Regular Security Staff Hours",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 4,
            Name = "Overtime Regular Security Hours",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 5,
            Name = "High Security Staff Hours",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 6,
            Name = "Overtime High Security Hours",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 7,
            Name = "Instructor Hours",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 8,
            Name = "Instructor Overtime",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 9,
            Name = "PTO Hours",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 10,
            Name = "PTO Overtime",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 11,
            Name = "Branch Directed Hours",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 12,
            Name = "Branch Directed Overtime",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 13,
            Name = "Self Development Hours",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 14,
            Name = "Cell Block Hours",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 15,
            Name = "Overtime Staff Hours",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 16,
            Name = "Jury Administration Hours",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 17,
            Name = "Coroner Jury Administration Hours",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 18,
            Name = "Level 1 Staff Hours",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 19,
            Name = "Level 1 Overtime Hours",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 20,
            Name = "Level 2 Staff Hours",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 21,
            Name = "Level 2 Overtime Hours",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 22,
            Name = "Level 3 Staff Hours",
            UnitOfMeasure = "hours",
        },
        new()
        {
            Id = 23,
            Name = "Level 3 Overtime Hours",
            UnitOfMeasure = "hours",
        },
        // Count (IDs 24-42)
        new()
        {
            Id = 24,
            Name = "Number of Jurors Summonsed",
            UnitOfMeasure = "count",
        },
        new()
        {
            Id = 25,
            Name = "Number of Jurors and Alternates Paid",
            UnitOfMeasure = "count",
        },
        new()
        {
            Id = 26,
            Name = "Number of Panels Created",
            UnitOfMeasure = "count",
        },
        new()
        {
            Id = 27,
            Name = "Coroner Jurors Summonsed",
            UnitOfMeasure = "count",
        },
        new()
        {
            Id = 28,
            Name = "Coroner Panels Created",
            UnitOfMeasure = "count",
        },
        new()
        {
            Id = 29,
            Name = "Number of Trips",
            UnitOfMeasure = "count",
        },
        new()
        {
            Id = 30,
            Name = "Level 1 Number of Trips",
            UnitOfMeasure = "count",
        },
        new()
        {
            Id = 31,
            Name = "Level 2 Number of Trips",
            UnitOfMeasure = "count",
        },
        new()
        {
            Id = 32,
            Name = "Level 3 Number of Trips",
            UnitOfMeasure = "count",
        },
        new()
        {
            Id = 33,
            Name = "Custodies - Number of Regulars",
            UnitOfMeasure = "count",
        },
        new()
        {
            Id = 34,
            Name = "Custodies - Number of SEG/PC/MH",
            UnitOfMeasure = "count",
        },
        new()
        {
            Id = 35,
            Name = "Number of Samples Taken",
            UnitOfMeasure = "count",
        },
        new()
        {
            Id = 36,
            Name = "Level 1 Air",
            UnitOfMeasure = "count",
        },
        new()
        {
            Id = 37,
            Name = "Level 1 Ground",
            UnitOfMeasure = "count",
        },
        new()
        {
            Id = 38,
            Name = "Level 2 Air",
            UnitOfMeasure = "count",
        },
        new()
        {
            Id = 39,
            Name = "Level 2 Ground",
            UnitOfMeasure = "count",
        },
        new()
        {
            Id = 40,
            Name = "Level 3 Air",
            UnitOfMeasure = "count",
        },
        new()
        {
            Id = 41,
            Name = "Level 3 Ground",
            UnitOfMeasure = "count",
        },
        new()
        {
            Id = 42,
            Name = "SEG/PC/MH",
            UnitOfMeasure = "count",
        },
        // Kilometres (IDs 43-46)
        new()
        {
            Id = 43,
            Name = "Number of km Travelled",
            UnitOfMeasure = "km",
        },
        new()
        {
            Id = 44,
            Name = "Level 1 Number of Ground km Travelled",
            UnitOfMeasure = "km",
        },
        new()
        {
            Id = 45,
            Name = "Level 2 Number of Ground km Travelled",
            UnitOfMeasure = "km",
        },
        new()
        {
            Id = 46,
            Name = "Level 3 Number of Ground km Travelled",
            UnitOfMeasure = "km",
        },
        // Dollars (ID 47)
        new()
        {
            Id = 47,
            Name = "Sum Total ($) Paid to Jurors and Alternates",
            UnitOfMeasure = "$",
        },
        // Count received/concluded (IDs 48-49)
        new()
        {
            Id = 48,
            Name = "Received",
            UnitOfMeasure = "count (received/concluded)",
        },
        new()
        {
            Id = 49,
            Name = "Concluded",
            UnitOfMeasure = "count (received/concluded)",
        },
    ];

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Updating stat metrics...");

        var createdCount = 0;
        var updatedCount = 0;

        foreach (var seed in SeedMetrics)
        {
            var existing = await dbContext.StatMetrics.FirstOrDefaultAsync(m => m.Id == seed.Id, cancellationToken);

            if (existing is null)
            {
                Logger.LogInformation("StatMetric with Id {Id} does not exist, adding it...", seed.Id);
                await dbContext.StatMetrics.AddAsync(seed, cancellationToken);
                createdCount++;
                continue;
            }

            Logger.LogInformation("Updating fields for StatMetric with Id {Id}...", seed.Id);
            existing.Name = seed.Name;
            existing.UnitOfMeasure = seed.UnitOfMeasure;
            updatedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        Logger.LogInformation(
            "StatMetric seeding complete. Created {CreatedCount}, updated {UpdatedCount}.",
            createdCount,
            updatedCount
        );
    }
}
