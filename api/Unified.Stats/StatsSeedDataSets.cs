using Microsoft.Extensions.Configuration;
using Unified.Authorization.Seeders;
using Unified.Common.Seeding;

namespace Unified.Stats;

public static class StatsSeedDataSets
{
    public const string StatsPermissionsDataSet = nameof(StatsPermissionsDataSet);

    public static IReadOnlyList<SeedDataSetDescriptor> All { get; } =
    [
        new(
            StatsPermissionsDataSet,
            [
                new PermissionSeedConfiguration
                {
                    Source = StatsPermissionsDataSet,
                    Definitions = StatsPermissionSeedData.Instance.Definitions,
                },
            ],
            RequiredFeature: "StatsModule",
            AvailableWhen: configuration => configuration.GetValue<bool>("FeatureFlags:StatsModule")
        ),
    ];
}