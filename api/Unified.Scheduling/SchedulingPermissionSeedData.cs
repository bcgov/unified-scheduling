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
            ],
        };
}
