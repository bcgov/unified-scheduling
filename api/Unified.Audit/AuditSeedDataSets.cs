using Unified.Authorization.Seeders;
using Unified.Common.Seeding;

namespace Unified.Audit;

public static class AuditSeedDataSets
{
    public const string AuditPermissionsDataSet = nameof(AuditPermissionsDataSet);

    public static IReadOnlyList<SeedDataSetDescriptor> All { get; } =
    [
        new(
            AuditPermissionsDataSet,
            [
                new PermissionSeedConfiguration
                {
                    Source = AuditPermissionsDataSet,
                    Definitions = AuditPermissionSeedData.Instance.Definitions,
                },
            ]
        ),
    ];
}
