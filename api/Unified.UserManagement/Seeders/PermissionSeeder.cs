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
        logger.LogInformation("Updating permissions from configured data sets...");

        var createdCount = 0;
        var updatedCount = 0;
        var dataSetConfigurations = configurations.ToArray();
        var seedDefinitions = dataSetConfigurations.SelectMany(config => config.Permissions).ToList();

        var duplicatePermissionIds = dataSetConfigurations
            .SelectMany(configuration => configuration.Permissions.Select(permission => (Permission: permission, configuration.Source)))
            .GroupBy(item => item.Permission.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"'{group.Key}' from {string.Join(", ", group.Select(item => item.Source).Distinct())}")
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (duplicatePermissionIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate permission seed IDs detected: {string.Join(", ", duplicatePermissionIds)}"
            );
        }

        logger.LogInformation(
            "Loaded {PermissionCount} permission seed entries from {ConfigurationCount} data-set configurations.",
            seedDefinitions.Count,
            dataSetConfigurations.Length
        );

        foreach (var seedDefinition in seedDefinitions)
        {
            var seedPermission = new Permission
            {
                Id = seedDefinition.Id,
                Group = seedDefinition.Group,
                Description = seedDefinition.Description,
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

            var hasChanges = false;

            if (!StringComparer.Ordinal.Equals(existingPermission.Group, seedPermission.Group))
            {
                existingPermission.Group = seedPermission.Group;
                hasChanges = true;
            }

            if (!StringComparer.Ordinal.Equals(existingPermission.Description, seedPermission.Description))
            {
                existingPermission.Description = seedPermission.Description;
                hasChanges = true;
            }

            if (hasChanges)
            {
                updatedCount++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Permission seeding complete. Created {CreatedCount}, updated {UpdatedCount}.",
            createdCount,
            updatedCount
        );
    }
}
