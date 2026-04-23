using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.UserManagement;

namespace Unified.UserManagement.Seeders;

/// <summary>
/// Seeder for the Role table.
/// </summary>
public class RoleSeeder(ILogger<RoleSeeder> logger) : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 2;

    public override string Name => "Role";

    private static readonly Role[] SeedRoles =
    [
        new()
        {
            Id = 1,
            Name = Role.Administrator,
            Description = "Administrator",
        },
        new()
        {
            Id = 2,
            Name = Role.Manager,
            Description = "Manager",
        },
        new()
        {
            Id = 3,
            Name = Role.Staff,
            Description = "Staff",
        },
    ];

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Updating roles...");

        var createdCount = 0;
        var updatedCount = 0;

        foreach (var seedRole in SeedRoles)
        {
            var existingRole = await dbContext
                .Roles.AsQueryable()
                .FirstOrDefaultAsync(r => r.Id == seedRole.Id, cancellationToken);

            if (existingRole is null)
            {
                Logger.LogInformation("Role with Id {Id} does not exist, adding it...", seedRole.Id);
                await dbContext.Roles.AddAsync(seedRole, cancellationToken);
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
}
