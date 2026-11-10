using Microsoft.EntityFrameworkCore;
using Unified.Common.Time;
using Unified.Common.Validation;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;

namespace Unified.Scheduling.Services;

internal static class AssignmentUniquenessValidator
{
    private readonly record struct LocationDetails(string Name, string TimeZoneId);

    private readonly record struct Key(
        int LocationId,
        string LocationName,
        int DefinitionId,
        string DefinitionName,
        DateOnly Date,
        Guid UserId
    );

    public static async Task EnsureAssignmentsAreUniqueAsync(
        UnifiedDbContext db,
        ITimeZoneService timeZoneService,
        IReadOnlyCollection<int> assignmentEntryIds,
        CancellationToken cancellationToken
    )
    {
        if (assignmentEntryIds.Count == 0)
            return;
        var candidates = await db
            .AssignmentEntries.AsNoTracking()
            .Include(entry => entry.Event)
            .Include(entry => entry.ShiftAssignmentEntries)
                .ThenInclude(link => link.Users)
            .Where(entry => assignmentEntryIds.Contains(entry.Id))
            .Where(entry => entry.Event != null && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
            .Where(entry => entry.ShiftAssignmentEntries.Any(link => link.Users.Any()))
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0)
            return;
        var locationIds = candidates
            .Select(entry =>
                entry.Event!.LocationId ?? throw new InvalidOperationException("An assignment location is required.")
            )
            .Distinct()
            .ToList();
        var locations = await db
            .Locations.AsNoTracking()
            .Where(item => locationIds.Contains(item.Id))
            .ToDictionaryAsync(
                item => item.Id,
                item => new LocationDetails(item.Name, item.Timezone),
                cancellationToken
            );
        var timeZones = locations.ToDictionary(
            pair => pair.Key,
            pair => timeZoneService.ResolveRequired(pair.Value.TimeZoneId)
        );
        var definitionIds = candidates.Select(candidate => candidate.AssignmentDefinitionId).Distinct().ToList();
        var definitionNames = await db
            .AssignmentDefinitions.AsNoTracking()
            .Where(item => definitionIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        IEnumerable<Key> GetKeys(AssignmentEntry entry)
        {
            var locationId = entry.Event!.LocationId!.Value;
            if (!timeZones.TryGetValue(locationId, out var timeZone))
                throw new InvalidOperationException($"Location {locationId} does not exist.");
            var local = timeZoneService.ToLocalUnspecified(entry.Event.StartAtUtc, timeZone);
            return entry
                .ShiftAssignmentEntries.SelectMany(link => link.Users)
                .Select(user => user.UserId)
                .Distinct()
                .Select(userId => new Key(
                    locationId,
                    locations[locationId].Name,
                    entry.AssignmentDefinitionId,
                    definitionNames[entry.AssignmentDefinitionId],
                    DateOnly.FromDateTime(local),
                    userId
                ));
        }
        var keys = candidates.SelectMany(GetKeys).ToList();
        var duplicate = keys.GroupBy(key => key).FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicate is Key duplicateKey)
            throw CreateDuplicateException(duplicateKey);
        var ranges = keys.Select(key =>
                timeZoneService.ConvertInclusiveLocalDateRangeToUtcRange(key.Date, key.Date, timeZones[key.LocationId])
            )
            .ToList();
        var earliest = ranges.Min(range => range.StartAtUtc);
        var latest = ranges.Max(range => range.EndAtUtc);
        var userIds = keys.Select(key => key.UserId).Distinct().ToList();
        var existing = await db
            .AssignmentEntries.AsNoTracking()
            .Include(entry => entry.Event)
            .Include(entry => entry.ShiftAssignmentEntries)
                .ThenInclude(link => link.Users)
            .Where(entry => !assignmentEntryIds.Contains(entry.Id))
            .Where(entry => definitionIds.Contains(entry.AssignmentDefinitionId))
            .Where(entry =>
                entry.ShiftAssignmentEntries.Any(link => link.Users.Any(user => userIds.Contains(user.UserId)))
            )
            .Where(entry =>
                entry.Event != null
                && entry.Event.LocationId.HasValue
                && locationIds.Contains(entry.Event.LocationId.Value)
                && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                && entry.Event.StartAtUtc >= earliest
                && entry.Event.StartAtUtc < latest
            )
            .ToListAsync(cancellationToken);
        var keySet = keys.ToHashSet();
        var conflict = existing.SelectMany(GetKeys).FirstOrDefault(keySet.Contains);
        if (conflict != default)
            throw CreateDuplicateException(conflict);
    }

    private static InformationValidationException CreateDuplicateException(Key key) =>
        new(
            $"An assignment already exists for a selected user at location {key.LocationName}, assignment definition {key.DefinitionName}, and date {key.Date:yyyy-MM-dd}."
        );
}
