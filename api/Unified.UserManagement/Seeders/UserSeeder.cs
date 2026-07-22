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
        var seedUsers = configurations.SelectMany(configuration => configuration.Users).ToArray();

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
            .SelectMany(configuration => configuration.Users.Select(user => (User: user, configuration.Source)))
            .ToArray();
        var errors = definitions.GroupBy(item => item.User.Id).Where(group => group.Count() > 1)
            .Select(group => $"Id '{group.Key}' from {string.Join(", ", group.Select(item => item.Source).Distinct())}")
            .Concat(
                definitions.GroupBy(item => item.User.IdirName, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1)
                    .Select(group => $"IdirName '{group.Key}' from {string.Join(", ", group.Select(item => item.Source).Distinct())}")
            )
            .ToArray();
        if (errors.Length > 0)
            throw new InvalidOperationException($"Duplicate user seed values detected: {string.Join(", ", errors)}");
    }
}
