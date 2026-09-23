using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Training;

namespace Unified.Training.Seeders;

public sealed class TrainingProfileSeeder(ILogger<TrainingProfileSeeder> logger) : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 2;

    public override string Name => "TrainingProfile";

    private static readonly DateTimeOffset SeedEffectiveDate = new(2026, 9, 14, 0, 0, 0, TimeSpan.Zero);

    private static readonly TrainingProfile[] SeedProfiles =
    [
        new()
        {
            Code = "CARBINE_OPERATOR",
            Description = "Carbine Operator",
            EffectiveDate = SeedEffectiveDate,
        },
    ];

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        foreach (var seedProfile in SeedProfiles)
        {
            var existingProfile = await dbContext
                .TrainingProfiles.AsQueryable()
                .FirstOrDefaultAsync(profile => profile.Code == seedProfile.Code, cancellationToken);

            if (existingProfile is null)
            {
                await dbContext.TrainingProfiles.AddAsync(seedProfile, cancellationToken);
                continue;
            }

            existingProfile.Description = seedProfile.Description;
            existingProfile.EffectiveDate = seedProfile.EffectiveDate;
            existingProfile.ExpiryDate = seedProfile.ExpiryDate;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
