using Microsoft.Extensions.Configuration;
using Unified.Authorization.Seeders;
using Unified.Common.Seeding;
using Unified.Stats.Seeders;

namespace Unified.Stats;

public static class StatsSeedDataSets
{
    public const string StatsPermissionsDataSet = nameof(StatsPermissionsDataSet);
    public const string BiOracleMappingsDataSet = nameof(BiOracleMappingsDataSet);

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
            RequiredFeature: "Stats:Enabled",
            AvailableWhen: configuration => configuration.GetValue<bool>("FeatureFlags:Stats:Enabled")
        ),
        new(
            BiOracleMappingsDataSet,
            [
                new BiOracleStatMappingSeedConfiguration
                {
                    Source = BiOracleMappingsDataSet,
                    Definitions = BiOracleStatMappingSeedData.Definitions,
                },
                new BiOracleLocationMappingSeedConfiguration
                {
                    Source = BiOracleMappingsDataSet,
                    Definitions = BiOracleLocationMappingSeedData.Definitions,
                },
            ]
        ),
    ];
}
