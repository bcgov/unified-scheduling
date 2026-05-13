using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Authorization;
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
public class PermissionSeeder(ILogger<PermissionSeeder> logger) : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 1;

    public override string Name => "Permission";

    private static readonly Permission[] SeedPermissions =
    [
        // Authentication
        new() { Id = nameof(Permissions.AuthLogin), Description = "Log in to the application" },
        // Users
        new() { Id = nameof(Permissions.UsersCreate), Description = "Create new users" },
        new() { Id = nameof(Permissions.UsersEdit), Description = "Edit existing users" },
        new() { Id = nameof(Permissions.UsersView), Description = "View users" },
        new() { Id = nameof(Permissions.UsersExpire), Description = "Expire users" },
        new() { Id = nameof(Permissions.UsersViewOtherProfiles), Description = "View other user profiles" },
        // Roles
        new() { Id = nameof(Permissions.RolesView), Description = "View roles" },
        new() { Id = nameof(Permissions.RolesCreateAndAssign), Description = "Create and assign roles" },
        new() { Id = nameof(Permissions.RolesEdit), Description = "Edit roles" },
        new() { Id = nameof(Permissions.RolesExpire), Description = "Expire roles" },
        // Types
        new() { Id = nameof(Permissions.TypesCreate), Description = "Create types" },
        new() { Id = nameof(Permissions.TypesEdit), Description = "Edit types" },
        new() { Id = nameof(Permissions.TypesExpire), Description = "Expire types" },
        // Shifts
        new() { Id = nameof(Permissions.ShiftsView), Description = "View shifts" },
        new() { Id = nameof(Permissions.ShiftsCreateAndAssign), Description = "Create and assign shifts" },
        new() { Id = nameof(Permissions.ShiftsEdit), Description = "Edit shifts" },
        new() { Id = nameof(Permissions.ShiftsExpire), Description = "Expire shifts" },
        new() { Id = nameof(Permissions.ShiftsImport), Description = "Import shifts" },
        new() { Id = nameof(Permissions.ShiftsViewAllFuture), Description = "View all future shifts" },
        // Schedule
        new() { Id = nameof(Permissions.ScheduleViewDistribute), Description = "View distribute schedule" },
        // Assignments
        new() { Id = nameof(Permissions.AssignmentsCreate), Description = "Create assignments" },
        new() { Id = nameof(Permissions.AssignmentsEdit), Description = "Edit assignments" },
        new() { Id = nameof(Permissions.AssignmentsExpire), Description = "Expire assignments" },
        // Duty Roster
        new() { Id = nameof(Permissions.DutyRosterView), Description = "View duty roster" },
        new() { Id = nameof(Permissions.DutyRosterViewFuture), Description = "View duty roster in the future" },
        // Duties
        new() { Id = nameof(Permissions.DutiesCreateAndAssign), Description = "Create and assign duties" },
        new() { Id = nameof(Permissions.DutiesEdit), Description = "Edit duties" },
        new() { Id = nameof(Permissions.DutiesExpire), Description = "Expire duties" },
        // Location
        new() { Id = nameof(Permissions.LocationViewHome), Description = "View home location" },
        new() { Id = nameof(Permissions.LocationViewAssigned), Description = "View assigned location" },
        new() { Id = nameof(Permissions.LocationViewRegion), Description = "View region" },
        new() { Id = nameof(Permissions.LocationViewProvince), Description = "View province" },
        new() { Id = nameof(Permissions.LocationExpire), Description = "Expire locations" },
        // Training
        new() { Id = nameof(Permissions.TrainingEditPast), Description = "Edit past training records" },
        new() { Id = nameof(Permissions.TrainingRemovePast), Description = "Remove past training records" },
        new() { Id = nameof(Permissions.TrainingAdjustExpiry), Description = "Adjust training expiry" },
        new() { Id = nameof(Permissions.TrainingExempt), Description = "Exempt from training requirements" },
        // IDIR
        new() { Id = nameof(Permissions.IdirEdit), Description = "Edit IDIR" },
        // Reports
        new() { Id = nameof(Permissions.ReportsGenerate), Description = "Generate reports" },
    ];

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        logger.LogInformation("Updating permissions...");

        var createdCount = 0;
        var updatedCount = 0;

        foreach (var seedPermission in SeedPermissions)
        {
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
