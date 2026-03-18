using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.UserManagement;

namespace Unified.UserManagement.Seeders;

/// <summary>
/// Seeder for the User table.
/// </summary>
public class UserSeeder(ILogger<UserSeeder> logger) : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 0;

    public override string Name => "User";

    private static readonly User[] SeedUsers =
    [
        new()
        {
            Id = User.SystemUser,
            IdirName = "SYSTEM",
            IsEnabled = false,
            FirstName = "System",
            LastName = "System",
        },
    ];

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Updating users...");

        var createdCount = 0;
        var updatedCount = 0;

        foreach (var seedUser in SeedUsers)
        {
            var existingUser = await dbContext
                .Users.AsQueryable()
                .FirstOrDefaultAsync(user => user.Id == seedUser.Id, cancellationToken);

            if (existingUser is null)
            {
                Logger.LogInformation("User with {Id} does not exist, adding it...", seedUser.Id);
                await dbContext.Users.AddAsync(seedUser, cancellationToken);
                createdCount++;
                continue;
            }

            Logger.LogInformation("Updating fields for user with {Id}...", seedUser.Id);
            existingUser.IdirName = seedUser.IdirName;
            existingUser.IsEnabled = seedUser.IsEnabled;
            existingUser.FirstName = seedUser.FirstName;
            existingUser.LastName = seedUser.LastName;
            existingUser.Email = seedUser.Email;
            existingUser.Gender = seedUser.Gender;
            existingUser.BadgeNumber = seedUser.BadgeNumber;
            existingUser.Rank = seedUser.Rank;
            updatedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        Logger.LogInformation(
            "User seeding complete. Created {CreatedCount}, updated {UpdatedCount}.",
            createdCount,
            updatedCount
        );
    }
}
