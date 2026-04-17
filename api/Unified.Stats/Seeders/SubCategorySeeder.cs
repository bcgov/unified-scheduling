using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Stats;

namespace Unified.Stats.Seeders;

public class SubCategorySeeder(ILogger<SubCategorySeeder> logger) : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 12;

    public override string Name => "SubCategory";

    private static readonly SubCategory[] SeedSubCategories =
    [
        // Court Security - Non-Supervision (CategoryId = 1), IDs 1-17
        new()
        {
            Id = 1,
            CategoryId = 1,
            Name = "Coroners Inquest",
            DisplayOrder = 1,
        },
        new()
        {
            Id = 2,
            CategoryId = 1,
            Name = "Court of Appeal",
            DisplayOrder = 2,
        },
        new()
        {
            Id = 3,
            CategoryId = 1,
            Name = "Other Courts not listed",
            DisplayOrder = 3,
        },
        new()
        {
            Id = 4,
            CategoryId = 1,
            Name = "Provincial Adult Criminal",
            DisplayOrder = 4,
        },
        new()
        {
            Id = 5,
            CategoryId = 1,
            Name = "Provincial Family",
            DisplayOrder = 5,
        },
        new()
        {
            Id = 6,
            CategoryId = 1,
            Name = "Provincial Small Claims",
            DisplayOrder = 6,
        },
        new()
        {
            Id = 7,
            CategoryId = 1,
            Name = "Provincial Youth",
            DisplayOrder = 7,
        },
        new()
        {
            Id = 8,
            CategoryId = 1,
            Name = "Rovers",
            DisplayOrder = 8,
        },
        new()
        {
            Id = 9,
            CategoryId = 1,
            Name = "Search Gate",
            DisplayOrder = 9,
        },
        new()
        {
            Id = 10,
            CategoryId = 1,
            Name = "Supreme Civil Jury",
            DisplayOrder = 10,
        },
        new()
        {
            Id = 11,
            CategoryId = 1,
            Name = "Supreme Civil Jury Deliberations",
            DisplayOrder = 11,
        },
        new()
        {
            Id = 12,
            CategoryId = 1,
            Name = "Supreme Civil Non Jury",
            DisplayOrder = 12,
        },
        new()
        {
            Id = 13,
            CategoryId = 1,
            Name = "Supreme Criminal Jury",
            DisplayOrder = 13,
        },
        new()
        {
            Id = 14,
            CategoryId = 1,
            Name = "Supreme Criminal Jury Deliberations",
            DisplayOrder = 14,
        },
        new()
        {
            Id = 15,
            CategoryId = 1,
            Name = "Supreme Criminal Non Jury",
            DisplayOrder = 15,
        },
        new()
        {
            Id = 16,
            CategoryId = 1,
            Name = "Supreme Family",
            DisplayOrder = 16,
        },
        new()
        {
            Id = 17,
            CategoryId = 1,
            Name = "Video Conferencing",
            DisplayOrder = 17,
        },
        // Circuit court related travel - Non-Supervision (CategoryId = 2), ID 18
        new()
        {
            Id = 18,
            CategoryId = 2,
            Name = "General",
            DisplayOrder = 1,
        },
        // Coroner Jury Administration (CategoryId = 3), ID 19
        new()
        {
            Id = 19,
            CategoryId = 3,
            Name = "General",
            DisplayOrder = 1,
        },
        // Criminal/Civil Jury Administration (CategoryId = 4), ID 20
        new()
        {
            Id = 20,
            CategoryId = 4,
            Name = "General",
            DisplayOrder = 1,
        },
        // Documents Civil/Family - Non-Supervision (CategoryId = 5), IDs 21-24
        new()
        {
            Id = 21,
            CategoryId = 5,
            Name = "Courts Orders",
            DisplayOrder = 1,
        },
        new()
        {
            Id = 22,
            CategoryId = 5,
            Name = "Documents",
            DisplayOrder = 2,
        },
        new()
        {
            Id = 23,
            CategoryId = 5,
            Name = "Others",
            DisplayOrder = 3,
        },
        new()
        {
            Id = 24,
            CategoryId = 5,
            Name = "Warrants",
            DisplayOrder = 4,
        },
        // Documents Criminal - Non-Supervision (CategoryId = 6), IDs 25-28
        new()
        {
            Id = 25,
            CategoryId = 6,
            Name = "Courts Orders",
            DisplayOrder = 1,
        },
        new()
        {
            Id = 26,
            CategoryId = 6,
            Name = "Documents",
            DisplayOrder = 2,
        },
        new()
        {
            Id = 27,
            CategoryId = 6,
            Name = "Others",
            DisplayOrder = 3,
        },
        new()
        {
            Id = 28,
            CategoryId = 6,
            Name = "Warrants",
            DisplayOrder = 4,
        },
        // Escorts Air - Hours-Trips - Non-Supervision (CategoryId = 7), ID 29
        new()
        {
            Id = 29,
            CategoryId = 7,
            Name = "Security level and hours",
            DisplayOrder = 1,
        },
        // Escorts Ground - Hours-Trips-Kilometres - Non-Supervision (CategoryId = 8), ID 30
        new()
        {
            Id = 30,
            CategoryId = 8,
            Name = "Security level and hours",
            DisplayOrder = 1,
        },
        // Escorts females - Escorted (CategoryId = 9), IDs 31-33
        new()
        {
            Id = 31,
            CategoryId = 9,
            Name = "Number of adult females",
            DisplayOrder = 1,
        },
        new()
        {
            Id = 32,
            CategoryId = 9,
            Name = "Number of Federal females",
            DisplayOrder = 2,
        },
        new()
        {
            Id = 33,
            CategoryId = 9,
            Name = "Number of youth females",
            DisplayOrder = 3,
        },
        // Escorts males - Escorted (CategoryId = 10), IDs 34-36
        new()
        {
            Id = 34,
            CategoryId = 10,
            Name = "Number of Adult males",
            DisplayOrder = 1,
        },
        new()
        {
            Id = 35,
            CategoryId = 10,
            Name = "Number of Federal males",
            DisplayOrder = 2,
        },
        new()
        {
            Id = 36,
            CategoryId = 10,
            Name = "Number of Youth males",
            DisplayOrder = 3,
        },
        // Holding area/cellblock - Non-Supervision (CategoryId = 11), IDs 37-43
        new()
        {
            Id = 37,
            CategoryId = 11,
            Name = "Adult females - Provincial",
            DisplayOrder = 1,
        },
        new()
        {
            Id = 38,
            CategoryId = 11,
            Name = "Adult males - Provincial",
            DisplayOrder = 2,
        },
        new()
        {
            Id = 39,
            CategoryId = 11,
            Name = "Federal Females",
            DisplayOrder = 3,
        },
        new()
        {
            Id = 40,
            CategoryId = 11,
            Name = "Federal Males",
            DisplayOrder = 4,
        },
        new()
        {
            Id = 41,
            CategoryId = 11,
            Name = "Hours",
            DisplayOrder = 5,
        },
        new()
        {
            Id = 42,
            CategoryId = 11,
            Name = "Youth females - Provincial",
            DisplayOrder = 6,
        },
        new()
        {
            Id = 43,
            CategoryId = 11,
            Name = "Youth males - Provincial",
            DisplayOrder = 7,
        },
        // Other - Non-Supervision (CategoryId = 12), IDs 44-49
        new()
        {
            Id = 44,
            CategoryId = 12,
            Name = "Administration/other duties",
            DisplayOrder = 1,
        },
        new()
        {
            Id = 45,
            CategoryId = 12,
            Name = "CPIC checks for Jury Administration",
            DisplayOrder = 2,
        },
        new()
        {
            Id = 46,
            CategoryId = 12,
            Name = "CPIC/JUSTIN",
            DisplayOrder = 3,
        },
        new()
        {
            Id = 47,
            CategoryId = 12,
            Name = "Completion of Incident (SIR)",
            DisplayOrder = 4,
        },
        new()
        {
            Id = 48,
            CategoryId = 12,
            Name = "DNA samples",
            DisplayOrder = 5,
        },
        new()
        {
            Id = 49,
            CategoryId = 12,
            Name = "Vehicle Management",
            DisplayOrder = 6,
        },
        // PIO/SIO - Non-Supervision (CategoryId = 13), ID 50
        new()
        {
            Id = 50,
            CategoryId = 13,
            Name = "General",
            DisplayOrder = 1,
        },
        // Training - Non-Supervision (CategoryId = 14), IDs 51-52
        new()
        {
            Id = 51,
            CategoryId = 14,
            Name = "Instruction",
            DisplayOrder = 1,
        },
        new()
        {
            Id = 52,
            CategoryId = 14,
            Name = "Student",
            DisplayOrder = 2,
        },
        // Court Security - Supervision (CategoryId = 15), IDs 53-69
        new()
        {
            Id = 53,
            CategoryId = 15,
            Name = "Coroners Inquest",
            DisplayOrder = 1,
        },
        new()
        {
            Id = 54,
            CategoryId = 15,
            Name = "Court of Appeal",
            DisplayOrder = 2,
        },
        new()
        {
            Id = 55,
            CategoryId = 15,
            Name = "Other Courts not listed",
            DisplayOrder = 3,
        },
        new()
        {
            Id = 56,
            CategoryId = 15,
            Name = "Provincial Adult Criminal",
            DisplayOrder = 4,
        },
        new()
        {
            Id = 57,
            CategoryId = 15,
            Name = "Provincial Family",
            DisplayOrder = 5,
        },
        new()
        {
            Id = 58,
            CategoryId = 15,
            Name = "Provincial Small Claims",
            DisplayOrder = 6,
        },
        new()
        {
            Id = 59,
            CategoryId = 15,
            Name = "Provincial Youth",
            DisplayOrder = 7,
        },
        new()
        {
            Id = 60,
            CategoryId = 15,
            Name = "Rovers",
            DisplayOrder = 8,
        },
        new()
        {
            Id = 61,
            CategoryId = 15,
            Name = "Search Gate",
            DisplayOrder = 9,
        },
        new()
        {
            Id = 62,
            CategoryId = 15,
            Name = "Supreme Civil Jury",
            DisplayOrder = 10,
        },
        new()
        {
            Id = 63,
            CategoryId = 15,
            Name = "Supreme Civil Jury Deliberations",
            DisplayOrder = 11,
        },
        new()
        {
            Id = 64,
            CategoryId = 15,
            Name = "Supreme Civil Non Jury",
            DisplayOrder = 12,
        },
        new()
        {
            Id = 65,
            CategoryId = 15,
            Name = "Supreme Criminal Jury",
            DisplayOrder = 13,
        },
        new()
        {
            Id = 66,
            CategoryId = 15,
            Name = "Supreme Criminal Jury Deliberations",
            DisplayOrder = 14,
        },
        new()
        {
            Id = 67,
            CategoryId = 15,
            Name = "Supreme Criminal Non Jury",
            DisplayOrder = 15,
        },
        new()
        {
            Id = 68,
            CategoryId = 15,
            Name = "Supreme Family",
            DisplayOrder = 16,
        },
        new()
        {
            Id = 69,
            CategoryId = 15,
            Name = "Video Conferencing",
            DisplayOrder = 17,
        },
        // Circuit court related travel - Supervision (CategoryId = 16), ID 70
        new()
        {
            Id = 70,
            CategoryId = 16,
            Name = "Hours",
            DisplayOrder = 1,
        },
        // Documents Civil/Family - Supervision (CategoryId = 17), IDs 71-74
        new()
        {
            Id = 71,
            CategoryId = 17,
            Name = "Courts Orders",
            DisplayOrder = 1,
        },
        new()
        {
            Id = 72,
            CategoryId = 17,
            Name = "Documents",
            DisplayOrder = 2,
        },
        new()
        {
            Id = 73,
            CategoryId = 17,
            Name = "Others",
            DisplayOrder = 3,
        },
        new()
        {
            Id = 74,
            CategoryId = 17,
            Name = "Warrants",
            DisplayOrder = 4,
        },
        // Documents Criminal - Supervision (CategoryId = 18), IDs 75-78
        new()
        {
            Id = 75,
            CategoryId = 18,
            Name = "Courts Orders",
            DisplayOrder = 1,
        },
        new()
        {
            Id = 76,
            CategoryId = 18,
            Name = "Documents",
            DisplayOrder = 2,
        },
        new()
        {
            Id = 77,
            CategoryId = 18,
            Name = "Others",
            DisplayOrder = 3,
        },
        new()
        {
            Id = 78,
            CategoryId = 18,
            Name = "Warrants",
            DisplayOrder = 4,
        },
        // Escorts Air - Hours-Trips - Supervision (CategoryId = 19), ID 79
        new()
        {
            Id = 79,
            CategoryId = 19,
            Name = "Hours (Level 1, 2, 3)",
            DisplayOrder = 1,
        },
        // Escorts Ground - Hours-Trips-Kilometres - Supervision (CategoryId = 20), ID 80
        new()
        {
            Id = 80,
            CategoryId = 20,
            Name = "Hours (Level 1, 2, 3)",
            DisplayOrder = 1,
        },
        // Holding area/cellblock - Supervision (CategoryId = 21), ID 81
        new()
        {
            Id = 81,
            CategoryId = 21,
            Name = "Hours",
            DisplayOrder = 1,
        },
        // Jury Administration - Supervision (CategoryId = 22), ID 82
        new()
        {
            Id = 82,
            CategoryId = 22,
            Name = "Hours",
            DisplayOrder = 1,
        },
        // Other - Supervision (CategoryId = 23), IDs 83-88
        new()
        {
            Id = 83,
            CategoryId = 23,
            Name = "Administration/other duties",
            DisplayOrder = 1,
        },
        new()
        {
            Id = 84,
            CategoryId = 23,
            Name = "CPIC checks for Jury Administration",
            DisplayOrder = 2,
        },
        new()
        {
            Id = 85,
            CategoryId = 23,
            Name = "CPIC/JUSTIN",
            DisplayOrder = 3,
        },
        new()
        {
            Id = 86,
            CategoryId = 23,
            Name = "Completion of Incident (SIR)",
            DisplayOrder = 4,
        },
        new()
        {
            Id = 87,
            CategoryId = 23,
            Name = "DNA samples",
            DisplayOrder = 5,
        },
        new()
        {
            Id = 88,
            CategoryId = 23,
            Name = "Vehicle Management",
            DisplayOrder = 6,
        },
        // PIO/SIO - Supervision (CategoryId = 24), ID 89
        new()
        {
            Id = 89,
            CategoryId = 24,
            Name = "General",
            DisplayOrder = 1,
        },
        // Training - Supervision (CategoryId = 25), IDs 90-91
        new()
        {
            Id = 90,
            CategoryId = 25,
            Name = "Instruction",
            DisplayOrder = 1,
        },
        new()
        {
            Id = 91,
            CategoryId = 25,
            Name = "Student",
            DisplayOrder = 2,
        },
    ];

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Updating sub-categories...");

        var createdCount = 0;
        var updatedCount = 0;

        foreach (var seed in SeedSubCategories)
        {
            var existing = await dbContext.SubCategories.FirstOrDefaultAsync(sc => sc.Id == seed.Id, cancellationToken);

            if (existing is null)
            {
                Logger.LogInformation("SubCategory with Id {Id} does not exist, adding it...", seed.Id);
                await dbContext.SubCategories.AddAsync(seed, cancellationToken);
                createdCount++;
                continue;
            }

            Logger.LogInformation("Updating fields for SubCategory with Id {Id}...", seed.Id);
            existing.CategoryId = seed.CategoryId;
            existing.Name = seed.Name;
            existing.DisplayOrder = seed.DisplayOrder;
            updatedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        Logger.LogInformation(
            "SubCategory seeding complete. Created {CreatedCount}, updated {UpdatedCount}.",
            createdCount,
            updatedCount
        );
    }
}
