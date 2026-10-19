using Microsoft.EntityFrameworkCore;
using Unified.Common.Time;
using Unified.Common.Validation;
using Unified.Db;
using Unified.Db.Models.Calendar;

namespace Unified.Scheduling.Services;

internal static class AssignmentUniquenessValidator
{
    internal readonly record struct Candidate(int LocationId, int AssignmentDefinitionId, DateTimeOffset StartAtUtc);

    private readonly record struct LocationDetails(string Name, string TimeZoneId);

    private readonly record struct Key(
        int LocationId,
        string LocationName,
        int DefinitionId,
        string DefinitionName,
        DateOnly Date
    );

    public static Candidate CreateCandidate(int definitionId, Event eventEntity) =>
        new(
            eventEntity.LocationId ?? throw new InvalidOperationException("An assignment location is required."),
            definitionId,
            eventEntity.StartAtUtc
        );

    public static async Task EnsureAssignmentsAreUniqueAsync(
        UnifiedDbContext db,
        ITimeZoneService timeZoneService,
        IReadOnlyCollection<Candidate> candidates,
        IReadOnlyCollection<int> excludedIds,
        CancellationToken cancellationToken
    )
    {
        if (candidates.Count == 0)
            return;
        var locationIds = candidates.Select(candidate => candidate.LocationId).Distinct().ToList();
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
        Key GetKey(Candidate candidate)
        {
            if (!timeZones.TryGetValue(candidate.LocationId, out var timeZone))
                throw new InvalidOperationException($"Location {candidate.LocationId} does not exist.");
            var local = timeZoneService.ToLocalUnspecified(candidate.StartAtUtc, timeZone);
            return new Key(
                candidate.LocationId,
                locations[candidate.LocationId].Name,
                candidate.AssignmentDefinitionId,
                definitionNames[candidate.AssignmentDefinitionId],
                DateOnly.FromDateTime(local)
            );
        }
        var keys = candidates.Select(GetKey).ToList();
        var duplicate = keys.GroupBy(key => key).FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicate is Key duplicateKey)
            throw CreateDuplicateException(duplicateKey);
        var ranges = keys.Select(key =>
                timeZoneService.ConvertInclusiveLocalDateRangeToUtcRange(key.Date, key.Date, timeZones[key.LocationId])
            )
            .ToList();
        var earliest = ranges.Min(range => range.StartAtUtc);
        var latest = ranges.Max(range => range.EndAtUtc);
        var existing = await db
            .AssignmentEntries.AsNoTracking()
            .Include(entry => entry.Event)
            .Where(entry => !excludedIds.Contains(entry.Id))
            .Where(entry => definitionIds.Contains(entry.AssignmentDefinitionId))
            .Where(entry =>
                entry.Event != null
                && entry.Event.LocationId.HasValue
                && locationIds.Contains(entry.Event.LocationId.Value)
                && entry.Event.StatusTypeCode != Unified.Db.Models.Calendar.CalendarEventStatusTypeCodes.Cancelled
                && entry.Event.StartAtUtc >= earliest
                && entry.Event.StartAtUtc < latest
            )
            .ToListAsync(cancellationToken);
        var keySet = keys.ToHashSet();
        var conflict = existing
            .Select(entry => GetKey(CreateCandidate(entry.AssignmentDefinitionId, entry.Event!)))
            .FirstOrDefault(keySet.Contains);
        if (conflict != default)
            throw CreateDuplicateException(conflict);
    }

    private static ConflictValidationException CreateDuplicateException(Key key) =>
        new(
            new Dictionary<string, string[]>
            {
                ["AssignmentDefinitionId"] =
                [
                    $"An assignment already exists for location {key.LocationName}, assignment definition {key.DefinitionName}, and date {key.Date:yyyy-MM-dd}.",
                ],
            }
        );
}
