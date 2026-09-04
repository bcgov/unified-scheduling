using Microsoft.Extensions.Configuration;
using Unified.Authorization.Seeders;
using Unified.Common.Seeding;

namespace Unified.Reporting;

public static class ReportingSeedDataSets
{
    public const string ReportingPermissionsDataSet = nameof(ReportingPermissionsDataSet);

    public static IReadOnlyList<SeedDataSetDescriptor> All { get; } =
    [
        new(
            ReportingPermissionsDataSet,
            [
                new PermissionSeedConfiguration
                {
                    Source = ReportingPermissionsDataSet,
                    Definitions = ReportingPermissionSeedData.Instance.Definitions,
                },
            ]
            ,
            RequiredFeature: "Reporting:Enabled",
            AvailableWhen: configuration => configuration.GetValue<bool>("FeatureFlags:Reporting:Enabled")
        ),
    ];
}
