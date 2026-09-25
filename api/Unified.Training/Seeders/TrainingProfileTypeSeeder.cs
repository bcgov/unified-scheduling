using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Training;

namespace Unified.Training.Seeders;

public sealed class TrainingProfileTypeSeeder(
    ILogger<TrainingProfileTypeSeeder> logger,
    IEnumerable<TrainingProfileTypeSeedConfiguration> configurations
) : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 2;

    public override string Name => "TrainingProfileType";

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        ValidateDefinitions(configurations);
        var seedProfiles = configurations.SelectMany(configuration => configuration.Definitions).ToArray();

        foreach (var seedProfile in seedProfiles)
        {
            var existingProfile = await dbContext
                .TrainingProfileTypes.AsQueryable()
                .FirstOrDefaultAsync(profile => profile.Code == seedProfile.Code, cancellationToken);

            if (existingProfile is null)
            {
                await dbContext.TrainingProfileTypes.AddAsync(
                    new TrainingProfileType { Code = seedProfile.Code, Name = seedProfile.Name },
                    cancellationToken
                );
                continue;
            }

            if (existingProfile.Name != seedProfile.Name)
            {
                existingProfile.Name = seedProfile.Name;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateDefinitions(IEnumerable<TrainingProfileTypeSeedConfiguration> configurations)
    {
        var definitions = configurations
            .SelectMany(configuration =>
                configuration.Definitions.Select(profile => (Definition: profile, configuration.Source))
            )
            .ToArray();

        SeedDefinitionValidator.ThrowIfDuplicateValues(
            definitions,
            "training profile type",
            (profile => profile.Code, "Code", StringComparer.OrdinalIgnoreCase)
        );
    }
}
