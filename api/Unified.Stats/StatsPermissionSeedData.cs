using Unified.Authorization;
using Unified.Common.Seeding;

namespace Unified.Stats;

/// <summary>
/// Static permission seed data owned by the Stats module.
/// </summary>
public static class StatsPermissionSeedData
{
    private const string PermissionGroupStats = "Stats";

    public static PermissionSeedConfiguration Configuration { get; } =
        new()
        {
            Permissions =
            [
                new()
                {
                    Group = PermissionGroupStats,
                    Id = nameof(Permissions.StatsRecordsEnterForOthers),
                    Description = "Enter stat records on behalf of other users",
                },
                new()
                {
                    Group = PermissionGroupStats,
                    Id = nameof(Permissions.DashboardView),
                    Description = "View the scheduler dashboard with consolidated team time entry data",
                },
                new()
                {
                    Group = PermissionGroupStats,
                    Id = nameof(Permissions.DashboardSignOff),
                    Description = "Sign off on time entries from the scheduler dashboard",
                },
                new()
                {
                    Group = PermissionGroupStats,
                    Id = nameof(Permissions.DashboardSubmit),
                    Description = "Submit time entries to the data pipeline from the scheduler dashboard",
                },
                new()
                {
                    Group = PermissionGroupStats,
                    Id = nameof(Permissions.StatsOverrideSignedOff),
                    Description = "Edit time entries that have already been signed off",
                },
            ],
        };
}
