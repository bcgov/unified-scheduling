using Unified.Common.Seeding;

namespace Unified.UserManagement;

public static class SheriffLocationSeedData
{
    public static IReadOnlyList<LocationSeedDefinition> Locations { get; } =
    [
        Location(1, "SS1", "Office of Professional Standards"),
        Location(2, "SS2", "Sheriff Provincial Operation Centre"),
        Location(3, "SS3", "Central Float Pool"),
        Location(4, "SS4", "Integrated Threat Assessment Unit", regionId: 100),
        Location(5, "SS5", "Office of the Chief Sheriff", regionId: 101),
        Location(6, "SS6", "South Okanagan Escort Centre", justinCode: "4882"),
        Location(7, "SS7", "Training Section", regionId: 100),
        Location(9, "SS9", "Recruitment Office", regionId: 100),
        Location(10, "SS10", "Provincial Programs", regionId: 100),
    ];

    private static LocationSeedDefinition Location(
        int id,
        string agencyId,
        string name,
        string? justinCode = null,
        int? regionId = null
    ) =>
        new()
        {
            Id = id,
            AgencyId = agencyId,
            Name = name,
            JustinCode = justinCode,
            RegionId = regionId,
            Timezone = "America/Vancouver",
        };
}
