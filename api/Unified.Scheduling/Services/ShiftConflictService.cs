using Microsoft.EntityFrameworkCore;
using Unified.Common.Time;
using Unified.Common.Validation;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;

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
        var awayUsers = await loanOutAvailabilityService.GetAwayUserIdsAsync(entries, cancellationToken);
        var candidates = CreateCandidates(entries, awayUsers);
        if (candidates.Count == 0)
            return;

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

        EnsureCandidatesDoNotConflictWithEachOther(candidates, locationTimeZones, userNames);

        var existingCandidates = await GetExistingCandidatesAsync(candidates, excludedShiftEntryIds, cancellationToken);
        var existingByUserAndLocation = existingCandidates.ToLookup(candidate =>
            (candidate.UserId, candidate.LocationId)
        );
        foreach (var candidate in candidates)
        {
            var conflict = existingByUserAndLocation[(candidate.UserId, candidate.LocationId)]
                .Select(existing => GetConflict(candidate, existing, locationTimeZones))
                .FirstOrDefault(result => result.HasValue);
            if (conflict.HasValue)
                throw CreateConflictException(candidate, conflict.Value, userNames);
        }
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
                    .Users.Where(user => !(awayUsers.GetValueOrDefault(entry) ?? []).Contains(user.UserId))
                    .Select(user => new ShiftConflictCandidate(
                        entry.Id,
                        user.UserId,
                        entry.Event!.LocationId,
                        entry.Event.StartAtUtc,
                        entry.Event.EndAtUtc,
                        entry.Event.TimeZoneId
                    ))
            )
            .ToList();

    private void EnsureCandidatesDoNotConflictWithEachOther(
        IReadOnlyCollection<ShiftConflictCandidate> candidates,
        IReadOnlyDictionary<int, TimeZoneInfo> locationTimeZones,
        IReadOnlyDictionary<Guid, string> userNames
    )
    {
        foreach (var group in candidates.GroupBy(candidate => (candidate.UserId, candidate.LocationId)))
        {
            var ordered = group.OrderBy(candidate => candidate.StartAtUtc).ToList();
            var startDates = new HashSet<DateOnly>();
            foreach (var candidate in ordered)
            {
                if (!startDates.Add(GetLocalDate(candidate, locationTimeZones)))
                    throw CreateConflictException(candidate, ShiftConflictKind.StartDate, userNames);
            }
            for (var i = 1; i < ordered.Count; i++)
            {
                var conflict = GetConflict(ordered[i - 1], ordered[i], locationTimeZones);
                if (conflict.HasValue)
                    throw CreateConflictException(ordered[i], conflict.Value, userNames);
            }
        }
    }

    private ShiftConflictKind? GetConflict(
        ShiftConflictCandidate first,
        ShiftConflictCandidate second,
        IReadOnlyDictionary<int, TimeZoneInfo> locationTimeZones
    )
    {
        if (
            first.UserId != second.UserId
            || first.LocationId != second.LocationId
            || (first.ShiftEntryId > 0 && first.ShiftEntryId == second.ShiftEntryId)
        )
            return null;
        if (GetLocalDate(first, locationTimeZones) == GetLocalDate(second, locationTimeZones))
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

    private static ConflictValidationException CreateConflictException(
        ShiftConflictCandidate candidate,
        ShiftConflictKind conflict,
        IReadOnlyDictionary<Guid, string> userNames
    )
    {
        var name = userNames.GetValueOrDefault(candidate.UserId, candidate.UserId.ToString());
        var message =
            conflict == ShiftConflictKind.StartDate
                ? $"{name} already has a shift at this location on this shift start date."
                : $"{name} already has a shift at this location that overlaps this shift.";
        return new ConflictValidationException(new Dictionary<string, string[]> { ["UserIds"] = [message] });
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
        int ShiftEntryId,
        Guid UserId,
        int? LocationId,
        DateTimeOffset StartAtUtc,
        DateTimeOffset? EndAtUtc,
        string? TimeZoneId
    );

    private enum ShiftConflictKind
    {
        StartDate,
        Overlap,
    }
}
