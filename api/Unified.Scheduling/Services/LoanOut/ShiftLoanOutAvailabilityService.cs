using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class ShiftLoanOutAvailabilityService(
    UnifiedDbContext db,
    ICalendarTimeZoneResolver timeZoneResolver,
    ITimeZoneService timeZoneService,
    Func<IShiftService> shiftServiceFactory
)
{
    public async Task<IReadOnlyDictionary<ShiftEntry, IReadOnlyCollection<Guid>>> GetAwayUserIdsAsync(
        IReadOnlyCollection<ShiftEntry> entries,
        CancellationToken cancellationToken,
        IReadOnlyCollection<int>? excludedLoanOutIds = null
    )
    {
        var result = new Dictionary<ShiftEntry, IReadOnlyCollection<Guid>>();
        var candidates = entries
            .Where(entry =>
                entry.Event is not null && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
            )
            .ToList();
        if (candidates.Count == 0)
            return result;
        var userIds = candidates.SelectMany(entry => entry.Users).Select(user => user.UserId).Distinct().ToList();
        var homeLocations = await db
            .Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.HomeLocationId, cancellationToken);
        var locationIds = candidates.Select(entry => entry.Event!.LocationId).Distinct().ToList();
        var locations = await db
            .Locations.AsNoTracking()
            .Where(location => locationIds.Contains(location.Id))
            .ToDictionaryAsync(location => location.Id, location => location.Timezone, cancellationToken);
        var ranges = candidates
            .Where(entry => entry.Event!.LocationId.HasValue && locations.ContainsKey(entry.Event.LocationId.Value))
            .ToDictionary(
                entry => entry,
                entry =>
                {
                    var zone = timeZoneService.ResolveRequired(locations[entry.Event!.LocationId!.Value]);
                    var date = DateOnly.FromDateTime(timeZoneService.ToLocalUnspecified(entry.Event.StartAtUtc, zone));
                    return timeZoneService.ConvertInclusiveLocalDateRangeToUtcRange(date, date, zone);
                }
            );
        if (ranges.Count == 0)
            return result;
        var rangeStart = ranges.Values.Min(range => range.StartAtUtc);
        var rangeEnd = ranges.Values.Max(range => range.EndAtUtc);
        var excludedIds = excludedLoanOutIds ?? [];
        var loanOuts = await db
            .LoanOutEntries.AsNoTracking()
            .Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry =>
                !excludedIds.Contains(entry.Id)
                && entry.Event != null
                && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                && entry.Event.StartAtUtc < rangeEnd
                && (
                    entry.Event.EndAtUtc.HasValue
                        ? entry.Event.EndAtUtc > rangeStart
                        : entry.Event.StartAtUtc >= rangeStart
                )
                && entry.Users.Any(user => userIds.Contains(user.UserId))
            )
            .ToListAsync(cancellationToken);

        foreach (var entry in candidates)
        {
            var eventEntity = entry.Event!;
            if (eventEntity.LocationId is not int locationId || !ranges.TryGetValue(entry, out var range))
                continue;
            var awayUsers = entry
                .Users.Where(user =>
                    loanOuts.Any(loanOut =>
                        loanOut.LoanOutLocationId != locationId
                        && IsFromLocation(loanOut, user.UserId, locationId, homeLocations)
                        && loanOut.Users.Any(item => item.UserId == user.UserId)
                        && loanOut.Event!.StartAtUtc < range.EndAtUtc
                        && (
                            loanOut.Event.EndAtUtc.HasValue
                                ? loanOut.Event.EndAtUtc > range.StartAtUtc
                                : loanOut.Event.StartAtUtc >= range.StartAtUtc
                        )
                    )
                )
                .Select(user => user.UserId)
                .ToList();
            result[entry] = awayUsers;
        }
        return result;
    }

    public async Task<IReadOnlyList<IReadOnlyCollection<Guid>>> GetAssignableUserIdsByOccurrenceAsync(
        IReadOnlyCollection<Guid> userIds,
        IReadOnlyCollection<ShiftAssignmentLoanOutContext> assignmentOccurrences,
        int? locationId,
        string? timeZoneId,
        CancellationToken cancellationToken = default
    )
    {
        if (assignmentOccurrences.Count == 0)
            return [];

        var locationTimeZoneId = locationId.HasValue
            ? await db
                .Locations.AsNoTracking()
                .Where(location => location.Id == locationId.Value)
                .Select(location => location.Timezone)
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var timeZone = timeZoneResolver.Resolve(locationId.HasValue ? null : timeZoneId, locationTimeZoneId);
        var ranges = assignmentOccurrences.Select(assignment => GetAssignmentDayRange(assignment, timeZone)).ToList();

        var rangeStartAtUtc = ranges.Min(range => range.StartAtUtc);
        var rangeEndAtUtc = ranges.Max(range => range.EndAtUtc);
        IReadOnlyList<LoanOutEntry> loanOutEntries =
            userIds.Count == 0
                ? []
                : await db
                    .LoanOutEntries.AsNoTracking()
                    .Include(entry => entry.Event)
                    .Include(entry => entry.Users)
                    .Where(entry =>
                        entry.Event != null
                        && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                        && (!locationId.HasValue || entry.LoanOutLocationId != locationId.Value)
                        && entry.Users.Any(user => userIds.Contains(user.UserId))
                        && entry.Event.StartAtUtc < rangeEndAtUtc
                        && (
                            entry.Event.EndAtUtc.HasValue
                                ? entry.Event.EndAtUtc.Value > rangeStartAtUtc
                                : entry.Event.StartAtUtc >= rangeStartAtUtc
                        )
                    )
                    .ToListAsync(cancellationToken);

        var homeLocations =
            locationId.HasValue && loanOutEntries.Any(entry => entry.Event!.LocationId is null)
                ? await db
                    .Users.AsNoTracking()
                    .Where(user => userIds.Contains(user.Id))
                    .ToDictionaryAsync(user => user.Id, user => user.HomeLocationId, cancellationToken)
                : new Dictionary<Guid, int?>();
        return ranges
            .Select(range =>
            {
                var loanedOutUserIds = loanOutEntries
                    .Where(entry =>
                        entry.Event is Event loanOutEvent && Overlaps(loanOutEvent, range.StartAtUtc, range.EndAtUtc)
                    )
                    .SelectMany(entry =>
                        entry.Users.Where(user =>
                            !locationId.HasValue || IsFromLocation(entry, user.UserId, locationId.Value, homeLocations)
                        )
                    )
                    .Select(user => user.UserId)
                    .ToHashSet();
                return (IReadOnlyCollection<Guid>)userIds.Where(userId => !loanedOutUserIds.Contains(userId)).ToList();
            })
            .ToList();
    }

    public async Task<bool> HasShiftAtLoanOutLocationAsync(
        IEnumerable<LoanOutEntry> loanOutEntries,
        int loanOutLocationId,
        CancellationToken cancellationToken
    )
    {
        var entries = loanOutEntries.Where(entry => entry.Event is not null && entry.Users.Count > 0).ToList();
        if (entries.Count == 0)
            return false;

        var locationTimeZoneId = await db
            .Locations.AsNoTracking()
            .Where(location => location.Id == loanOutLocationId)
            .Select(location => location.Timezone)
            .SingleOrDefaultAsync(cancellationToken);
        var timeZone = timeZoneResolver.Resolve(null, locationTimeZoneId);
        var ranges = entries
            .Select(entry => new
            {
                UserIds = entry.Users.Select(user => user.UserId).ToHashSet(),
                Range = GetAssignmentDayRange(
                    new ShiftAssignmentLoanOutContext(entry.Event!.StartAtUtc, entry.Event.EndAtUtc),
                    timeZone
                ),
            })
            .ToList();
        var rangeStartAtUtc = ranges.Min(item => item.Range.StartAtUtc);
        var rangeEndAtUtc = ranges.Max(item => item.Range.EndAtUtc);
        var shifts = await shiftServiceFactory()
            .GetShiftEntriesAsync(
                new ShiftEntryQueryParams
                {
                    LocationId = loanOutLocationId,
                    StartAtUtc = rangeStartAtUtc,
                    EndAtUtc = rangeEndAtUtc,
                },
                cancellationToken
            );

        return shifts.Any(shift =>
            ranges.Any(item => shift.UserIds.Any(item.UserIds.Contains) && Overlaps(shift, item.Range))
        );
    }

    internal static bool IsFromLocation(
        LoanOutEntry entry,
        Guid userId,
        int locationId,
        IReadOnlyDictionary<Guid, int?> homeLocations
    ) => (entry.Event!.LocationId ?? homeLocations.GetValueOrDefault(userId)) == locationId;

    private static bool Overlaps(ShiftEntryResponse shift, UtcDateRange range) =>
        shift.EndAtUtc.HasValue
            ? shift.EndAtUtc.Value > range.StartAtUtc && shift.StartAtUtc < range.EndAtUtc
            : shift.StartAtUtc >= range.StartAtUtc && shift.StartAtUtc < range.EndAtUtc;

    internal static bool Overlaps(Event eventEntity, DateTimeOffset rangeStartAtUtc, DateTimeOffset rangeEndAtUtc) =>
        eventEntity.EndAtUtc.HasValue
            ? eventEntity.EndAtUtc.Value > rangeStartAtUtc && eventEntity.StartAtUtc < rangeEndAtUtc
            : eventEntity.StartAtUtc >= rangeStartAtUtc && eventEntity.StartAtUtc < rangeEndAtUtc;

    internal UtcDateRange GetAssignmentDayRange(ShiftAssignmentLoanOutContext assignment, TimeZoneInfo timeZone)
    {
        var lastAssignmentInstant =
            assignment.AssignmentEndAtUtc is DateTimeOffset endAtUtc && endAtUtc > assignment.AssignmentStartAtUtc
                ? endAtUtc.AddTicks(-1)
                : assignment.AssignmentStartAtUtc;
        var startDate = DateOnly.FromDateTime(
            timeZoneService.ToLocalUnspecified(assignment.AssignmentStartAtUtc, timeZone)
        );
        var endDate = DateOnly.FromDateTime(timeZoneService.ToLocalUnspecified(lastAssignmentInstant, timeZone));
        return timeZoneService.ConvertInclusiveLocalDateRangeToUtcRange(startDate, endDate, timeZone);
    }
}
