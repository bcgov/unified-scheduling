using Unified.Common.Seeding;
using Unified.UserManagement.Seeders;

namespace Unified.UserManagement;

public sealed class SheriffRegionSeedData : ISeedData<RegionSeedDefinition>
{
    public static ISeedData<RegionSeedDefinition> Instance { get; } = new SheriffRegionSeedData();

    private SheriffRegionSeedData() { }

    public IReadOnlyList<RegionSeedDefinition> Definitions { get; } =
    [
        new()
        {
            Id = 100,
            Name = "Central Programs"
        },
        new()
        {
            Id = 101,
            Name = "Office of the Chief Sheriff"
        },
    ];
}
