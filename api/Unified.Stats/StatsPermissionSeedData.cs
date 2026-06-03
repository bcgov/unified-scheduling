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
            ],
        };
}
