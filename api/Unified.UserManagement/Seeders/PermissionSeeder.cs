using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.UserManagement;

namespace Unified.UserManagement.Seeders;

/// <summary>
/// Seeder for the Permission table.
///
/// Permissions are the source of truth for what actions exist in the system.
/// They are never created through the API — only seeded here and updated via PUT /api/permissions/{id}.
///
/// </summary>
public class PermissionSeeder(ILogger<PermissionSeeder> logger, IEnumerable<PermissionSeedConfiguration> configurations)
    : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 1;

    public override string Name => "Permission";

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        logger.LogInformation("Updating permissions from module configuration...");

        var createdCount = 0;
        var updatedCount = 0;
        var moduleConfigurations = configurations.ToArray();
        var seedDefinitions = moduleConfigurations
            .SelectMany(config => config.Permissions)
            .GroupBy(permission => permission.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        logger.LogInformation(
            "Loaded {PermissionCount} permission seed entries from {ConfigurationCount} module configurations.",
            seedDefinitions.Length,
            moduleConfigurations.Length
        );

        foreach (var seedPermissionDefinition in seedDefinitions)
        {
            var seedPermission = new Permission
            {
                Id = seedPermissionDefinition.Id,
                Description = seedPermissionDefinition.Description,
            };

            var existingPermission = await dbContext
                .Permissions.AsQueryable()
                .FirstOrDefaultAsync(p => p.Id == seedPermission.Id, cancellationToken);

            if (existingPermission is null)
            {
                logger.LogInformation("Permission with Id '{Id}' does not exist, adding it...", seedPermission.Id);
                await dbContext.Permissions.AddAsync(seedPermission, cancellationToken);
                createdCount++;
                continue;
            }

            updatedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Permission seeding complete. Created {CreatedCount}, skipped {UpdatedCount} (already customised).",
            createdCount,
            updatedCount
        );
    }
}
