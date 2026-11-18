using Microsoft.Extensions.Configuration;
using Unified.Authorization.Seeders;
using Unified.Common.Seeding;

namespace Unified.DataMigration;

public static class DataMigrationSeedDataSets
{
    public const string DataMigrationPermissionsDataSet = nameof(DataMigrationPermissionsDataSet);

    public static IReadOnlyList<SeedDataSetDescriptor> All { get; } =
    [
        new(
            DataMigrationPermissionsDataSet,
            [
                new PermissionSeedConfiguration
                {
                    Source = DataMigrationPermissionsDataSet,
                    Definitions = MigrationPermissions.Configuration.Definitions,
                },
            ],
            RequiredFeature: "DataMigration:Enabled",
            AvailableWhen: configuration => configuration.GetValue<bool>("FeatureFlags:DataMigration:Enabled")
        ),
    ];
}
