using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.UserManagement;

namespace Unified.UserManagement.Seeders;

/// <summary>
/// Seeder for the User table.
/// </summary>
public class UserSeeder(ILogger<UserSeeder> logger, IEnumerable<UserSeedConfiguration> configurations)
    : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 0;

    public override string Name => "User";

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Updating users...");

        ValidateDefinitions(configurations);
        var seedUsers = configurations.SelectMany(configuration => configuration.Definitions).ToArray();

        var createdCount = 0;
        var updatedCount = 0;

        foreach (var seedUser in seedUsers)
        {
            var existingUser = await dbContext
                .Users.AsQueryable()
                .FirstOrDefaultAsync(user => user.Id == seedUser.Id, cancellationToken);

            if (existingUser is null)
            {
                Logger.LogInformation("User with {Id} does not exist, adding it...", seedUser.Id);
                await dbContext.Users.AddAsync(
                    new User
                    {
                        Id = seedUser.Id,
                        IdirName = seedUser.IdirName,
                        IsEnabled = seedUser.IsEnabled,
                        FirstName = seedUser.FirstName,
                        LastName = seedUser.LastName,
                        BadgeNumber = seedUser.BadgeNumber,
                    },
                    cancellationToken
                );
                createdCount++;
                continue;
            }

            Logger.LogInformation("Updating fields for user with {Id}...", seedUser.Id);
            existingUser.IdirName = seedUser.IdirName;
            existingUser.IsEnabled = seedUser.IsEnabled;
            existingUser.FirstName = seedUser.FirstName;
            existingUser.LastName = seedUser.LastName;
            existingUser.BadgeNumber = seedUser.BadgeNumber;

            updatedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        Logger.LogInformation(
            "User seeding complete. Created {CreatedCount}, updated {UpdatedCount}.",
            createdCount,
            updatedCount
        );
    }

    private static void ValidateDefinitions(IEnumerable<UserSeedConfiguration> configurations)
    {
        var definitions = configurations
            .SelectMany(configuration =>
                configuration.Definitions.Select(user => (Definition: user, configuration.Source))
            )
            .ToArray();
        SeedDefinitionValidator.ThrowIfDuplicateValues(
            definitions,
            "user",
            (user => user.Id.ToString(), "Id", StringComparer.Ordinal),
            (user => user.IdirName, "IdirName", StringComparer.OrdinalIgnoreCase)
        );
    }
}
