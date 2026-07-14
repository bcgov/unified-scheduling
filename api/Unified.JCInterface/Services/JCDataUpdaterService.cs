using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JCCommon.Clients.LocationServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
// using Unified.Api.helpers;
using Unified.Common.Helpers.Extensions;
// using Unified.Api.infrastructure.exceptions;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.UserManagement;
using Unified.JCInterface.Options;
// using Unified.Db.Models.jc;
using Region = Unified.Db.Models.Region;

namespace Unified.JCInterface.Services
{
    public class JCDataUpdaterService(
        UnifiedDbContext dbContext,
        LocationServicesClient locationClient,
        ILogger<JCDataUpdaterService> logger,
        IOptionsMonitor<JCInterfaceOptions> jcInterfaceOptionsMonitor
    )
    {
        private JCInterfaceOptions jcInterfaceOptions { get; } = jcInterfaceOptionsMonitor.CurrentValue;

        // public async Task<bool> ShouldSynchronize()
        // {
        //     //Only a single row here.
        //     var jcSynchronization = await dbContext.JcSynchronization.FirstOrDefaultAsync(jc => jc.Id == 1);
        //     if (jcSynchronization == null)
        //     {
        //         await dbContext.JcSynchronization.AddAsync(new JcSynchronization { Id = 1, LastSynchronization = DateTimeOffset.UtcNow });
        //         await dbContext.SaveChangesAsync();
        //         return true;
        //     }

        //     if(jcInterfaceOptions.SkipLocationUpdates) return false;
        //     if (jcSynchronization.LastSynchronization.Add(jcInterfaceOptions.UpdateEvery) > DateTimeOffset.UtcNow) return false;
        //     jcSynchronization.LastSynchronization = DateTimeOffset.UtcNow;
        //     await dbContext.SaveChangesAsync();
        //     return true;
        // }

        public async Task SyncRegionsAsync()
        {
            var regions = await locationClient.RegionsAsync();

            var regionsDb = regions.SelectToList(r => new Region
            {
                JustinId = r.RegionId,
                Name = r.RegionName,
                CreatedById = User.SystemUser,
            });

            logger.LogInformation("Synchronizing {RegionCount} regions from JC-Interface", regionsDb.Count);

            await dbContext
                .Regions.UpsertRange(regionsDb)
                .On(v => v.JustinId)
                .WhenMatched(
                    (r, rnew) =>
                        new Region
                        {
                            Name = rnew.Name,
                            JustinId = rnew.JustinId,
                            UpdatedOn = DateTime.UtcNow,
                        }
                )
                .RunAsync();

            //Any regions that aren't on this list expire/disable for now. This is for regions that may have been deleted.
            if (jcInterfaceOptions.ExpireRegions)
            {
                var disableRegions = dbContext.Regions.WhereToList(r =>
                    r.ExpiryDate == null && regionsDb.All(rdb => rdb.JustinId != r.JustinId)
                );
                foreach (var disableRegion in disableRegions)
                {
                    logger.LogInformation("Expiring region {RegionId}", disableRegion.Id);
                    disableRegion.ExpiryDate = DateTime.UtcNow;
                    disableRegion.UpdatedOn = DateTime.UtcNow;
                    disableRegion.UpdatedById = User.SystemUser;
                }
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task SyncLocationsAsync()
        {
            var locationsDb = await GenerateLocationsAndLinkToRegions();

            logger.LogInformation("Synchronizing {LocationCount} locations from JC-Interface", locationsDb.Count);

            await dbContext
                .Locations.UpsertRange(locationsDb)
                .On(v => v.AgencyId)
                .WhenMatched(
                    (l, lnew) =>
                        new Location
                        {
                            Name = lnew.Name,
                            RegionId = lnew.RegionId,
                            UpdatedOn = DateTime.UtcNow,
                            Timezone = lnew.Timezone,
                        }
                )
                .RunAsync();

            //Set to false for now, because some Locations are brought in via Migration and not the JC-Interface.
            //Any Locations that aren't on this list expire/disable for now.  This is for locations that may have been deleted.
            if (jcInterfaceOptions.ExpireLocations)
            {
                var disableLocations = dbContext.Locations.WhereToList(l =>
                    l.ExpiryDate == null && locationsDb.All(rdb => rdb.AgencyId != l.AgencyId)
                );
                foreach (var disableLocation in disableLocations)
                {
                    logger.LogInformation("Expiring location {LocationId}", disableLocation.Id);
                    disableLocation.ExpiryDate = DateTime.UtcNow;
                    disableLocation.UpdatedOn = DateTime.UtcNow;
                    disableLocation.UpdatedById = User.SystemUser;
                }
                await dbContext.SaveChangesAsync();
            }

            if (jcInterfaceOptions.AssociateUsersWithNoLocationToVictoria)
            {
                var usersWithNoHomeLocation = dbContext.Users.Where(u => !u.HomeLocationId.HasValue);
                var victoriaLocation = dbContext
                    .Locations.AsNoTracking()
                    .FirstOrDefault(l => l.Name == "Victoria Law Courts");
                if (victoriaLocation == null)
                    return;
                foreach (var user in usersWithNoHomeLocation)
                {
                    logger.LogInformation(
                        "Assigning home location {LocationId} to user {UserId}",
                        victoriaLocation.Id,
                        user.Id
                    );
                    user.HomeLocationId = victoriaLocation.Id;
                }
                await dbContext.SaveChangesAsync();
            }

            //Associate Migrated Location to regions.
            await AssociateAdhocLocationToRegion();
        }

        public async Task SyncCourtRoomsAsync()
        {
            var courtRoomsLookups = await locationClient.LocationsRoomsAsync();
            //To list so we don't need to re-query on each select.
            var locations = dbContext.Locations.ToList();
            var courtRooms = courtRoomsLookups
                .SelectToList(cr => new CourtRoom
                {
                    Code = cr.Code,
                    LocationId = locations.FirstOrDefault(l => l.JustinCode == cr.Flex)?.Id,
                    EffectiveDate = DateTimeOffset.UtcNow,
                    CreatedById = User.SystemUser,
                })
                .WhereToList(cr => cr.LocationId != null);

            //Some court rooms, don't have a location. This should probably be fixed in the JC-Interface
            var courtRoomNoLocation = courtRoomsLookups.WhereToList(crl =>
                locations.All(l => l.JustinCode != crl.Flex)
            );
            logger.LogDebug(
                "Found {CourtRoomNoLocationCount} court rooms without a location",
                courtRoomNoLocation.Count
            );

            logger.LogInformation("Synchronizing {CourtRoomCount} court rooms from JC-Interface", courtRooms.Count);

            await dbContext
                .CourtRooms.UpsertRange(courtRooms)
                .On(v => new { v.Code, v.LocationId })
                .WhenMatched(
                    (cr, crNew) =>
                        new CourtRoom
                        {
                            Code = crNew.Code,
                            LocationId = crNew.LocationId,
                            EffectiveDate =
                                cr.EffectiveDate == default || cr.EffectiveDate == DateTimeOffset.MaxValue
                                    ? DateTimeOffset.UtcNow
                                    : cr.EffectiveDate,
                            UpdatedOn = DateTime.UtcNow,
                        }
                )
                .RunAsync();

            //Any court rooms that aren't from this list, expire/disable for now. This is for CourtRooms that may have been deleted.
            if (jcInterfaceOptions.ExpireCourtRooms)
            {
                var disableCourtRooms = dbContext.CourtRooms.WhereToList(cr =>
                    cr.ExpiryDate == null && !courtRooms.Any(c => c.Code == cr.Code && c.LocationId == cr.LocationId)
                );

                foreach (var disableCourtRoom in disableCourtRooms)
                {
                    logger.LogInformation("Expiring court room {CourtRoomId}", disableCourtRoom.Id);
                    disableCourtRoom.ExpiryDate = DateTime.UtcNow;
                    disableCourtRoom.UpdatedOn = DateTime.UtcNow;
                    disableCourtRoom.UpdatedById = User.SystemUser;
                }
                await dbContext.SaveChangesAsync();
            }
        }

        private async Task<List<Location>> GenerateLocationsAndLinkToRegions()
        {
            var regionDictionary = new Dictionary<int, ICollection<int>>();
            //RegionsRegionIdLocationsCodesAsync returns a LIST of locationIds.
            foreach (var region in dbContext.Regions)
            {
                if (region.JustinId == null)
                    continue;
                regionDictionary[region.Id] = await locationClient.RegionsRegionIdLocationsCodesAsync(
                    region.JustinId.ToString()
                );
            }

            //Reverse the dictionary and flatten.
            var locationToRegion = new Dictionary<string, int>();
            foreach (
                var (region, locationId) in regionDictionary.SelectMany(region =>
                    region.Value.Select(locationId => (region, locationId))
                )
            )
                locationToRegion[locationId.ToString()] = region.Key;

            var locationWithoutRegion = new List<Location>();
            var locations = await locationClient.LocationsAsync(null, true, false);
            var locationsDb = locations.SelectToList(loc =>
            {
                var regionId = locationToRegion.ContainsKey(loc.ShortDesc)
                    ? locationToRegion[loc.ShortDesc]
                    : null as int?;
                var location = new Location
                {
                    JustinCode = loc.ShortDesc,
                    Name = loc.LongDesc,
                    AgencyId = loc.Code,
                    RegionId = regionId,
                    CreatedById = User.SystemUser,
                };
                //Some locations don't have a region, this should be fixed in the JC-Interface.
                if (regionId == null)
                    locationWithoutRegion.Add(location);

                return location;
            });

            locationsDb = AssociateLocationToTimezone(locationsDb);

            logger.LogDebug(
                "Found {LocationWithoutRegionCount} locations without a region",
                locationWithoutRegion.Count
            );
            return locationsDb;
        }

        private async Task AssociateAdhocLocationToRegion()
        {
            var locationsToRegions = jcInterfaceOptions.NonJCInterfaceLocationRegions;

            foreach (var locationToRegion in locationsToRegions)
            {
                var location = await dbContext.Locations.FirstOrDefaultAsync(l => l.Name == locationToRegion.Key);
                if (location != null && location.RegionId == null)
                    location.RegionId = (
                        await dbContext
                            .Regions.AsNoTracking()
                            .FirstOrDefaultAsync(r => r.Name == locationToRegion.Value)
                    )?.Id;
            }
            await dbContext.SaveChangesAsync();
        }

        private List<Location> AssociateLocationToTimezone(List<Location> locations)
        {
            foreach (var location in locations)
            {
                var configurationSections = jcInterfaceOptions.LocationTimeZones;

                var otherTimezone = configurationSections
                    .FirstOrDefault(cs =>
                        cs.Value.Split(",")
                            .Select(s => s.Trim())
                            .Any(partialName => location.Name.Contains(partialName))
                    )
                    .Key;

                location.Timezone = otherTimezone ?? "America/Vancouver";
            }

            return locations;
        }
    }
}
