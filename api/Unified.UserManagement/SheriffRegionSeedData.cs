using Unified.Common.Seeding;

namespace Unified.UserManagement;

public static class SheriffRegionSeedData
{
    public static IReadOnlyList<RegionSeedDefinition> Regions { get; } =
        [
            new()
            {
                Id = 100,
                Name = "Central Programs",
                Code = "CP",
            },
            new()
            {
                Id = 101,
                Name = "Office of the Chief Sheriff",
                Code = "OCS",
            },
        ];
}
