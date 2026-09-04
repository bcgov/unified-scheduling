using Unified.Authorization;
using Unified.Authorization.Seeders;

namespace Unified.Scheduling;

/// <summary>
/// Static permission seed data owned by the Scheduling module.
/// </summary>
public static class SchedulingPermissionSeedData
{
    private const string PermissionGroupScheduling = "Scheduling";
    private const string PermissionSourceScheduling = "SchedulingModule";

    public static PermissionSeedConfiguration Configuration { get; } =
        new()
        {
            Source = PermissionSourceScheduling,
            Definitions =
            [
                new()
                {
                    Group = PermissionGroupScheduling,
                    Id = nameof(Permissions.ShiftsView),
                    Description = "View shifts",
                },
                new()
                {
                    Group = PermissionGroupScheduling,
                    Id = nameof(Permissions.ShiftsCreateAndAssign),
                    Description = "Create shifts and assign users to them",
                },
                new()
                {
                    Group = PermissionGroupScheduling,
                    Id = nameof(Permissions.ShiftsEdit),
                    Description = "Edit shifts",
                },
                new()
                {
                    Group = PermissionGroupScheduling,
                    Id = nameof(Permissions.ShiftsDelete),
                    Description = "Delete shifts",
                },
                new()
                {
                    Group = PermissionGroupScheduling,
                    Id = nameof(Permissions.ShiftsExpire),
                    Description = "Expire shifts",
                },
                new()
                {
                    Group = PermissionGroupScheduling,
                    Id = nameof(Permissions.AssignmentsView),
                    Description = "View assignments",
                },
                new()
                {
                    Group = PermissionGroupScheduling,
                    Id = nameof(Permissions.AssignmentsCreate),
                    Description = "Create assignments",
                },
                new()
                {
                    Group = PermissionGroupScheduling,
                    Id = nameof(Permissions.AssignmentsAssign),
                    Description = "Assign shifts to assignments",
                },
                new()
                {
                    Group = PermissionGroupScheduling,
                    Id = nameof(Permissions.AssignmentsEdit),
                    Description = "Edit assignments",
                },
                new()
                {
                    Group = PermissionGroupScheduling,
                    Id = nameof(Permissions.AssignmentsDelete),
                    Description = "Delete assignments",
                },
                new()
                {
                    Group = PermissionGroupScheduling,
                    Id = nameof(Permissions.AssignmentsExpire),
                    Description = "Expire assignments",
                },
            ],
        };
}
