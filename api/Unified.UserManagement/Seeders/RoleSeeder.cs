using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.UserManagement;

namespace Unified.UserManagement.Seeders;

/// <summary>
/// Seeder for the Role table.
/// </summary>
public class RoleSeeder(ILogger<RoleSeeder> logger, IEnumerable<RoleSeedConfiguration> configurations)
    : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 2;

    public override string Name => "Role";

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Updating roles...");

        ValidateDefinitions(configurations);
        var seedRoles = configurations.SelectMany(configuration => configuration.Roles).ToArray();

        var createdCount = 0;
        var updatedCount = 0;

        foreach (var seedRole in seedRoles)
        {
            var existingRole = await dbContext
                .Roles.AsQueryable()
                .FirstOrDefaultAsync(role => role.Id == seedRole.Id, cancellationToken);

            if (existingRole is null)
            {
                Logger.LogInformation("Role with Id {Id} does not exist, adding it...", seedRole.Id);
                await dbContext.Roles.AddAsync(
                    new Role
                    {
                        Id = seedRole.Id,
                        Name = seedRole.Name,
                        Description = seedRole.Description,
                    },
                    cancellationToken
                );
                createdCount++;
                continue;
            }

            Logger.LogInformation("Updating fields for role with Id {Id}...", seedRole.Id);
            existingRole.Name = seedRole.Name;
            existingRole.Description = seedRole.Description;
            updatedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        Logger.LogInformation(
            "Role seeding complete. Created {CreatedCount}, updated {UpdatedCount}.",
            createdCount,
            updatedCount
        );
    }

    private static void ValidateDefinitions(IEnumerable<RoleSeedConfiguration> configurations)
    {
        var definitions = configurations
            .SelectMany(configuration => configuration.Roles.Select(role => (Role: role, configuration.Source)))
            .ToArray();
        var errors = definitions.GroupBy(item => item.Role.Id).Where(group => group.Count() > 1)
            .Select(group => $"Id '{group.Key}' from {string.Join(", ", group.Select(item => item.Source).Distinct())}")
            .Concat(
                definitions.GroupBy(item => item.Role.Name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1)
                    .Select(group => $"Name '{group.Key}' from {string.Join(", ", group.Select(item => item.Source).Distinct())}")
            )
            .ToArray();
        if (errors.Length > 0)
            throw new InvalidOperationException($"Duplicate role seed values detected: {string.Join(", ", errors)}");
    }
}
