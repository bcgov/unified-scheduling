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
        new() { Id = Permissions.AuthLogin, Description = "Log in to the application" },

        // Users
        new() { Id = Permissions.UsersCreate, Description = "Create new users" },
        new() { Id = Permissions.UsersEdit, Description = "Edit existing users" },
        new() { Id = Permissions.UsersView, Description = "View users" },
        new() { Id = Permissions.UsersExpire, Description = "Expire users" },
        new() { Id = Permissions.UsersViewOtherProfiles, Description = "View other user profiles" },

        // Roles
        new() { Id = Permissions.RolesView, Description = "View roles" },
        new() { Id = Permissions.RolesCreateAndAssign, Description = "Create and assign roles" },
        new() { Id = Permissions.RolesEdit, Description = "Edit roles" },
        new() { Id = Permissions.RolesExpire, Description = "Expire roles" },

        // Types
        new() { Id = Permissions.TypesCreate, Description = "Create types" },
        new() { Id = Permissions.TypesEdit, Description = "Edit types" },
        new() { Id = Permissions.TypesExpire, Description = "Expire types" },

        // Shifts
        new() { Id = Permissions.ShiftsView, Description = "View shifts" },
        new() { Id = Permissions.ShiftsCreateAndAssign, Description = "Create and assign shifts" },
        new() { Id = Permissions.ShiftsEdit, Description = "Edit shifts" },
        new() { Id = Permissions.ShiftsExpire, Description = "Expire shifts" },
        new() { Id = Permissions.ShiftsImport, Description = "Import shifts" },
        new() { Id = Permissions.ShiftsViewAllFuture, Description = "View all future shifts" },

        // Schedule
        new() { Id = Permissions.ScheduleViewDistribute, Description = "View distribute schedule" },

        // Assignments
        new() { Id = Permissions.AssignmentsCreate, Description = "Create assignments" },
        new() { Id = Permissions.AssignmentsEdit, Description = "Edit assignments" },
        new() { Id = Permissions.AssignmentsExpire, Description = "Expire assignments" },

        // Duty Roster
        new() { Id = Permissions.DutyRosterView, Description = "View duty roster" },
        new() { Id = Permissions.DutyRosterViewFuture, Description = "View duty roster in the future" },

        // Duties
        new() { Id = Permissions.DutiesCreateAndAssign, Description = "Create and assign duties" },
        new() { Id = Permissions.DutiesEdit, Description = "Edit duties" },
        new() { Id = Permissions.DutiesExpire, Description = "Expire duties" },

        // Location
        new() { Id = Permissions.LocationViewHome, Description = "View home location" },
        new() { Id = Permissions.LocationViewAssigned, Description = "View assigned location" },
        new() { Id = Permissions.LocationViewRegion, Description = "View region" },
        new() { Id = Permissions.LocationViewProvince, Description = "View province" },
        new() { Id = Permissions.LocationExpire, Description = "Expire locations" },

        // Training
        new() { Id = Permissions.TrainingEditPast, Description = "Edit past training records" },
        new() { Id = Permissions.TrainingRemovePast, Description = "Remove past training records" },
        new() { Id = Permissions.TrainingAdjustExpiry, Description = "Adjust training expiry" },
        new() { Id = Permissions.TrainingExempt, Description = "Exempt from training requirements" },

        // IDIR
        new() { Id = Permissions.IdirEdit, Description = "Edit IDIR" },

        // Reports
        new() { Id = Permissions.ReportsGenerate, Description = "Generate reports" },
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
                logger.LogInformation(
                    "Permission with Id '{Id}' does not exist, adding it...",
                    seedPermission.Id
                );
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
