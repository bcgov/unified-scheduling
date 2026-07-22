using Unified.Common.Seeding;
using Unified.UserManagement.Seeders;

namespace Unified.UserManagement;

public sealed class SheriffLocationSeedData : ISeedData<LocationSeedDefinition>
{
    public static ISeedData<LocationSeedDefinition> Instance { get; } = new SheriffLocationSeedData();

    private SheriffLocationSeedData() { }

    public IReadOnlyList<LocationSeedDefinition> Definitions { get; } =
    [
        Location(1, "SS1", "Office of Professional Standards"),
        Location(2, "SS2", "Sheriff Provincial Operation Centre"),
        Location(3, "SS3", "Central Float Pool"),
        Location(4, "SS4", "Integrated Threat Assessment Unit", regionId: 100),
        Location(5, "SS5", "Office of the Chief Sheriff", regionId: 101),
        Location(6, "SS6", "South Okanagan Escort Centre", justinLocationCode: "4882"),
        Location(7, "SS7", "Training Section", regionId: 100),
        Location(9, "SS9", "Recruitment Office", regionId: 100),
        Location(10, "SS10", "Provincial Programs", regionId: 100),
    ];

    private static LocationSeedDefinition Location(
        int id,
        string agencyId,
        string name,
        string? justinLocationCode = null,
        int? regionId = null
    ) =>
        new()
        {
            Id = id,
            AgencyId = agencyId,
            Name = name,
            JustinLocationCode = justinLocationCode,
            RegionId = regionId,
            Timezone = "America/Vancouver",
        };
}
