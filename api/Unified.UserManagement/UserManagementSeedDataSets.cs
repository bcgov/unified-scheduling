using Unified.Authorization.Seeders;
using Unified.Common.Seeding;
using Unified.UserManagement.Seeders;

namespace Unified.UserManagement;

public static class UserManagementSeedDataSets
{
    public const string SheriffRegionLocationDataSet = nameof(SheriffRegionLocationDataSet);
    public const string PlatformSystemUserDataSet = nameof(PlatformSystemUserDataSet);
    public const string DefaultRolesDataSet = nameof(DefaultRolesDataSet);
    public const string UserManagementPermissionsDataSet = nameof(UserManagementPermissionsDataSet);

    public static IReadOnlyList<SeedDataSetDescriptor> All { get; } =
    [
        new(
            PlatformSystemUserDataSet,
            [
                new UserSeedConfiguration
                {
                    Source = PlatformSystemUserDataSet,
                    Definitions = PlatformSystemUserSeedData.Instance.Definitions,
                },
            ]
        ),
        new(
            DefaultRolesDataSet,
            [
                new RoleSeedConfiguration
                {
                    Source = DefaultRolesDataSet,
                    Definitions = DefaultRoleSeedData.Instance.Definitions,
                },
            ]
        ),
        new(
            UserManagementPermissionsDataSet,
            [
                new PermissionSeedConfiguration
                {
                    Source = UserManagementPermissionsDataSet,
                    Definitions = UserManagementPermissionSeedData.Instance.Definitions,
                },
            ]
        ),
        new(
            SheriffRegionLocationDataSet,
            [
                new RegionSeedConfiguration
                {
                    Source = SheriffRegionLocationDataSet,
                    Definitions = SheriffRegionSeedData.Instance.Definitions,
                },
                new LocationSeedConfiguration
                {
                    Source = SheriffRegionLocationDataSet,
                    Definitions = SheriffLocationSeedData.Instance.Definitions,
                },
            ]
        ),
    ];
}
