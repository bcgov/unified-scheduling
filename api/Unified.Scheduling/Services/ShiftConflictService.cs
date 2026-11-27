using Microsoft.EntityFrameworkCore;
using Unified.Common.Time;
using Unified.Common.Validation;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class ShiftConflictService(
    UnifiedDbContext db,
    ITimeZoneService timeZoneService,
    ShiftLoanOutAvailabilityService loanOutAvailabilityService
)
{
    private static readonly TimeSpan LocalStartDateConflictQueryBuffer = TimeSpan.FromDays(2);

    public async Task EnsureShiftsDoNotConflictAsync(
        IReadOnlyCollection<ShiftEntry> entries,
        IReadOnlyCollection<int> excludedShiftEntryIds,
        CancellationToken cancellationToken
    )
    {
        var conflicts = await FindConflictsAsync(entries, excludedShiftEntryIds, firstOnly: true, cancellationToken);
        if (conflicts.FirstOrDefault() is { } conflict)
            throw new ConflictValidationException(
                new Dictionary<string, string[]> { ["UserIds"] = [conflict.Message] }
            );
    }

    public Task<IReadOnlyCollection<ShiftPublicationBlocker>> CollectConflictsAsync(
        IReadOnlyCollection<ShiftEntry> entries,
        IReadOnlyCollection<int> excludedShiftEntryIds,
        CancellationToken cancellationToken
    ) => FindConflictsAsync(entries, excludedShiftEntryIds, firstOnly: false, cancellationToken);

    private async Task<IReadOnlyCollection<ShiftPublicationBlocker>> FindConflictsAsync(
        IReadOnlyCollection<ShiftEntry> entries,
        IReadOnlyCollection<int> excludedShiftEntryIds,
        bool firstOnly,
        CancellationToken cancellationToken
    )
    {
        var distinctEntries = entries.DistinctBy(entry => (entry.Id, Unsaved: entry.Id > 0 ? null : entry)).ToList();
        var awayUsers = await loanOutAvailabilityService.GetAwayUserIdsAsync(distinctEntries, cancellationToken);
        var candidates = CreateCandidates(distinctEntries, awayUsers);
        if (candidates.Count == 0)
            return [];

        var userIds = candidates.Select(candidate => candidate.UserId).Distinct().ToList();
        var users = await db
            .Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToListAsync(cancellationToken);
        var userNames = users.ToDictionary(user => user.Id, FormatUserName);
        var locationIds = candidates
            .Where(candidate => candidate.LocationId.HasValue)
            .Select(candidate => candidate.LocationId!.Value)
            .Distinct()
            .ToList();
        var locationTimeZones = await db
            .Locations.AsNoTracking()
            .Where(location => locationIds.Contains(location.Id))
            .ToDictionaryAsync(
                location => location.Id,
                location => timeZoneService.ResolveRequired(location.Timezone),
                cancellationToken
            );

        var pairs = GetCandidateConflicts(candidates, locationTimeZones);
        var findings = (firstOnly ? pairs.Take(1) : pairs)
            .Select(pair => CreateFinding(pair.First, pair.Second, pair.Kind, userNames))
            .ToList();
        if (firstOnly && findings.Count > 0)
            return findings;

        var excludedIds = excludedShiftEntryIds
            .Concat(candidates.Select(candidate => candidate.Shift.ShiftEntryId).Where(id => id > 0))
            .Distinct()
            .ToList();
        var existingCandidates = await GetExistingCandidatesAsync(candidates, excludedIds, cancellationToken);
        var existingByUserAndLocation = existingCandidates.ToLookup(candidate =>
            (candidate.UserId, candidate.LocationId)
        );
        foreach (var candidate in candidates)
        {
            var date = GetLocalDate(candidate, locationTimeZones);
            foreach (var existing in existingByUserAndLocation[(candidate.UserId, candidate.LocationId)])
            {
                if (GetConflict(candidate, existing, date, GetLocalDate(existing, locationTimeZones)) is not { } kind)
                    continue;
                findings.Add(CreateFinding(candidate, existing, kind, userNames));
                if (firstOnly)
                    return findings;
            }
        }
        return findings;
    }

    private async Task<IReadOnlyCollection<ShiftConflictCandidate>> GetExistingCandidatesAsync(
        IReadOnlyCollection<ShiftConflictCandidate> candidates,
        IReadOnlyCollection<int> excludedShiftEntryIds,
        CancellationToken cancellationToken
    )
    {
        var userIds = candidates.Select(candidate => candidate.UserId).Distinct().ToList();
        var locationIds = candidates.Select(candidate => candidate.LocationId).Distinct().ToList();
        var earliest = candidates.Min(candidate => candidate.StartAtUtc).Subtract(LocalStartDateConflictQueryBuffer);
        var query = db
            .ShiftEntries.AsNoTracking()
            .Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry => !excludedShiftEntryIds.Contains(entry.Id))
            .Where(entry => entry.Users.Any(user => userIds.Contains(user.UserId)))
            .Where(entry =>
                entry.Event != null
                && locationIds.Contains(entry.Event.LocationId)
                && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Draft
                && (entry.Event.EndAtUtc == null || entry.Event.EndAtUtc > earliest)
            );
        if (candidates.All(candidate => candidate.EndAtUtc.HasValue))
        {
            var latest = candidates.Max(candidate => candidate.EndAtUtc!.Value).Add(LocalStartDateConflictQueryBuffer);
            query = query.Where(entry => entry.Event != null && entry.Event.StartAtUtc < latest);
        }
        var entries = await query.ToListAsync(cancellationToken);
        var awayUsers = await loanOutAvailabilityService.GetAwayUserIdsAsync(entries, cancellationToken);
        return CreateCandidates(entries, awayUsers);
    }

    private static List<ShiftConflictCandidate> CreateCandidates(
        IEnumerable<ShiftEntry> entries,
        IReadOnlyDictionary<ShiftEntry, IReadOnlyCollection<Guid>> awayUsers
    ) =>
        entries
            .Where(entry => entry.Event?.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
            .SelectMany(entry =>
                entry
                    .Users.Select(user => user.UserId)
                    .Distinct()
                    .Where(userId => !(awayUsers.GetValueOrDefault(entry) ?? []).Contains(userId))
                    .Select(userId => new ShiftConflictCandidate(
                        new(entry.Id, entry.EventId, entry.Event!.Title, entry.Event.StartAtUtc, entry.Event.EndAtUtc),
                        userId,
                        entry.Event.LocationId,
                        entry.Event.TimeZoneId
                    ))
            )
            .ToList();

    private IEnumerable<(
        ShiftConflictCandidate First,
        ShiftConflictCandidate Second,
        ShiftConflictKind Kind
    )> GetCandidateConflicts(
        IReadOnlyCollection<ShiftConflictCandidate> candidates,
        IReadOnlyDictionary<int, TimeZoneInfo> locationTimeZones
    )
    {
        foreach (var group in candidates.GroupBy(candidate => (candidate.UserId, candidate.LocationId)))
        {
            var ordered = group.OrderBy(candidate => candidate.StartAtUtc).ToList();
            var dates = ordered.Select(candidate => GetLocalDate(candidate, locationTimeZones)).ToList();
            // Report same-date pairs first and exclude them from the overlap results.
            var earlierByDate = new Dictionary<DateOnly, List<ShiftConflictCandidate>>();
            for (var i = 0; i < ordered.Count; i++)
            {
                if (!earlierByDate.TryGetValue(dates[i], out var earlier))
                    earlierByDate.Add(dates[i], earlier = []);
                foreach (var previous in earlier)
                    yield return (previous, ordered[i], ShiftConflictKind.StartDate);
                earlier.Add(ordered[i]);
            }
            for (var i = 0; i < ordered.Count; i++)
            {
                for (var j = i + 1; j < ordered.Count; j++)
                {
                    if (ordered[i].EndAtUtc is { } end && ordered[j].StartAtUtc >= end)
                        break;
                    if (GetConflict(ordered[i], ordered[j], dates[i], dates[j]) == ShiftConflictKind.Overlap)
                        yield return (ordered[i], ordered[j], ShiftConflictKind.Overlap);
                }
            }
        }
    }

    private static ShiftConflictKind? GetConflict(
        ShiftConflictCandidate first,
        ShiftConflictCandidate second,
        DateOnly firstDate,
        DateOnly secondDate
    )
    {
        if (firstDate == secondDate)
            return ShiftConflictKind.StartDate;
        return IntervalsOverlap(first.StartAtUtc, first.EndAtUtc, second.StartAtUtc, second.EndAtUtc)
            ? ShiftConflictKind.Overlap
            : null;
    }

    private DateOnly GetLocalDate(
        ShiftConflictCandidate candidate,
        IReadOnlyDictionary<int, TimeZoneInfo> locationTimeZones
    )
    {
        var timeZone = timeZoneService.ResolveOrUtc(candidate.TimeZoneId);
        if (
            string.IsNullOrWhiteSpace(candidate.TimeZoneId)
            && candidate.LocationId.HasValue
            && locationTimeZones.TryGetValue(candidate.LocationId.Value, out var locationTimeZone)
        )
            timeZone = locationTimeZone;
        return DateOnly.FromDateTime(timeZoneService.ToLocalUnspecified(candidate.StartAtUtc, timeZone));
    }

    private static bool IntervalsOverlap(
        DateTimeOffset firstStart,
        DateTimeOffset? firstEnd,
        DateTimeOffset secondStart,
        DateTimeOffset? secondEnd
    ) => (!secondEnd.HasValue || firstStart < secondEnd.Value) && (!firstEnd.HasValue || secondStart < firstEnd.Value);

    private static ShiftPublicationBlocker CreateFinding(
        ShiftConflictCandidate first,
        ShiftConflictCandidate second,
        ShiftConflictKind conflict,
        IReadOnlyDictionary<Guid, string> userNames
    )
    {
        var name = userNames.GetValueOrDefault(first.UserId, first.UserId.ToString());
        var message =
            conflict == ShiftConflictKind.StartDate
                ? $"{name} already has a shift at this location on this shift start date."
                : $"{name} already has a shift at this location that overlaps this shift.";
        return new(first.UserId, conflict, first.Shift, second.Shift, message);
    }

    private static string FormatUserName(Unified.Db.Models.UserManagement.User user)
    {
        var fullName = string.Join(
                " ",
                new[] { user.FirstName, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))
            )
            .Trim();
        return !string.IsNullOrWhiteSpace(fullName) ? fullName
            : !string.IsNullOrWhiteSpace(user.IdirName) ? user.IdirName
            : user.Id.ToString();
    }

    private sealed record ShiftConflictCandidate(
        SchedulePublishShiftEventResponse Shift,
        Guid UserId,
        int? LocationId,
        string? TimeZoneId
    )
    {
        public DateTimeOffset StartAtUtc => Shift.StartAtUtc;
        public DateTimeOffset? EndAtUtc => Shift.EndAtUtc;
    }
}
