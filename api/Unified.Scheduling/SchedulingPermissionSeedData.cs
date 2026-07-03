using Unified.Authorization;
using Unified.Common.Seeding;

namespace Unified.Scheduling;

/// <summary>
/// Static permission seed data owned by the Scheduling module.
/// </summary>
public static class SchedulingPermissionSeedData
{
    private const string PermissionGroupScheduling = "Scheduling";

    public static PermissionSeedConfiguration Configuration { get; } =
        new()
        {
            Permissions =
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
                    Id = nameof(Permissions.ShiftsExpire),
                    Description = "Expire shifts",
                },
            ],
        };
}
