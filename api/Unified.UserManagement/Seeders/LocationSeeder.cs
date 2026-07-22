using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models;

namespace Unified.UserManagement.Seeders;

/// <summary>
/// Aggregates configured location data sets and upserts them into the Location table.
/// </summary>
public sealed class LocationSeeder(
    ILogger<LocationSeeder> logger,
    IEnumerable<LocationSeedConfiguration> configurations
) : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 2;

    public override string Name => "Location";

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        ValidateDefinitions(configurations);
        var seedLocations = configurations.SelectMany(configuration => configuration.Locations).ToArray();

        var createdCount = 0;
        var updatedCount = 0;

        foreach (var seedLocation in seedLocations)
        {
            var existingLocation = await dbContext.Locations.FirstOrDefaultAsync(
                location => location.Id == seedLocation.Id,
                cancellationToken
            );

            if (existingLocation is null)
            {
                Logger.LogInformation("Location with Id {Id} does not exist, adding it...", seedLocation.Id);
                await dbContext.Locations.AddAsync(
                    new Location
                    {
                        Id = seedLocation.Id,
                        AgencyId = seedLocation.AgencyId,
                        Name = seedLocation.Name,
                        JustinCode = seedLocation.JustinCode,
                        RegionId = seedLocation.RegionId,
                        Timezone = seedLocation.Timezone,
                    },
                    cancellationToken
                );
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

    private static void ValidateDefinitions(IEnumerable<LocationSeedConfiguration> configurations)
    {
        var definitions = configurations
            .SelectMany(configuration => configuration.Locations.Select(location => (Location: location, configuration.Source)))
            .ToArray();
        var errors = DuplicateErrors(definitions, item => item.Location.Id.ToString(), "Id", StringComparer.Ordinal)
            .Concat(DuplicateErrors(definitions, item => item.Location.AgencyId, "AgencyId", StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (errors.Length > 0)
        {
            throw new InvalidOperationException($"Duplicate location seed values detected: {string.Join(", ", errors)}");
        }
    }

    private static IEnumerable<string> DuplicateErrors(
        IEnumerable<(LocationSeedDefinition Location, string Source)> definitions,
        Func<(LocationSeedDefinition Location, string Source), string> keySelector,
        string keyName,
        IEqualityComparer<string> comparer
    ) => definitions
        .GroupBy(keySelector, comparer)
        .Where(group => group.Count() > 1)
        .Select(group => $"{keyName} '{group.Key}' from {string.Join(", ", group.Select(item => item.Source).Distinct())}");
}
