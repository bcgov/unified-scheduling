using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models;

namespace Unified.UserManagement.Seeders;

/// <summary>
/// Seeder for the Location table, sourced from Sheriff Scheduling location data.
/// </summary>
public class LocationSeeder(ILogger<LocationSeeder> logger) : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 2;

    public override string Name => "Location";

    private static readonly Location[] SeedLocations =
    [
        new()
        {
            Id = 1,
            AgencyId = "SS1",
            Name = "Office of Professional Standards",
            Timezone = "America/Vancouver",
        },
        new()
        {
            Id = 2,
            AgencyId = "SS2",
            Name = "Sheriff Provincial Operation Centre",
            Timezone = "America/Vancouver",
        },
        new()
        {
            Id = 3,
            AgencyId = "SS3",
            Name = "Central Float Pool",
            Timezone = "America/Vancouver",
        },
        new()
        {
            Id = 4,
            AgencyId = "SS4",
            Name = "Integrated Threat Assessment Unit",
            Timezone = "America/Vancouver",
            RegionId = 100,
        },
        new()
        {
            Id = 5,
            AgencyId = "SS5",
            Name = "Office of the Chief Sheriff",
            Timezone = "America/Vancouver",
            RegionId = 101,
        },
        new()
        {
            Id = 6,
            AgencyId = "SS6",
            Name = "South Okanagan Escort Centre",
            Timezone = "America/Vancouver",
            JustinCode = "4882",
        },
        new()
        {
            Id = 7,
            AgencyId = "SS7",
            Name = "Training Section",
            Timezone = "America/Vancouver",
            RegionId = 100,
        },
        new()
        {
            Id = 9,
            AgencyId = "SS9",
            Name = "Recruitment Office",
            Timezone = "America/Vancouver",
            RegionId = 100,
        },
        new()
        {
            Id = 10,
            AgencyId = "SS10",
            Name = "Provincial Programs",
            Timezone = "America/Vancouver",
            RegionId = 100,
        },
    ];

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Updating locations...");

        var createdCount = 0;
        var updatedCount = 0;

        foreach (var seedLocation in SeedLocations)
        {
            var existingLocation = await dbContext
                .Locations.AsQueryable()
                .FirstOrDefaultAsync(l => l.Id == seedLocation.Id, cancellationToken);

            if (existingLocation is null)
            {
                Logger.LogInformation("Location with Id {Id} does not exist, adding it...", seedLocation.Id);
                await dbContext.Locations.AddAsync(seedLocation, cancellationToken);
                createdCount++;
                continue;
            }

            Logger.LogInformation("Updating fields for location with Id {Id}...", seedLocation.Id);
            existingLocation.AgencyId = seedLocation.AgencyId;
            existingLocation.Name = seedLocation.Name;
            existingLocation.JustinCode = seedLocation.JustinCode;
            existingLocation.RegionId = seedLocation.RegionId;
            existingLocation.Timezone = seedLocation.Timezone;
            updatedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        Logger.LogInformation(
            "Location seeding complete. Created {CreatedCount}, updated {UpdatedCount}.",
            createdCount,
            updatedCount
        );
    }
}
