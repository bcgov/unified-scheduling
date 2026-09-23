using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Training;

namespace Unified.Training.Seeders;

public sealed class TrainingProfileSeeder(ILogger<TrainingProfileSeeder> logger) : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 2;

    public override string Name => "TrainingProfileType";

    private static readonly TrainingProfileType[] SeedProfiles =
    [
        new() { Code = "CARBINE_OPERATOR", Name = "Carbine Operator" },
    ];

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        foreach (var seedProfile in SeedProfiles)
        {
            var existingProfile = await dbContext
                .TrainingProfileTypes.AsQueryable()
                .FirstOrDefaultAsync(profile => profile.Code == seedProfile.Code, cancellationToken);

            if (existingProfile is null)
            {
                await dbContext.TrainingProfileTypes.AddAsync(seedProfile, cancellationToken);
                continue;
            }

            if (existingProfile.Name != seedProfile.Name)
            {
                existingProfile.Name = seedProfile.Name;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
