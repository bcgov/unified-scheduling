using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Unified.Common.Scheduling;
using Unified.Common.Scheduling.Models;
using Unified.Common.Time;
using Unified.Common.Validation;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Guards;

public static class LoanOutUserShiftGuard
{
    public static Task EnsureCanScheduleAsync(
        UnifiedDbContext db,
        ITimeZoneService timeZoneService,
        ISchedulingCalendarEventDataProvider<SchedulingCalendarEventResponse>? loanOutProvider,
        int? locationId,
        IReadOnlyCollection<Guid> userIds,
        DateTimeOffset startAtUtc,
        DateTimeOffset? endAtUtc,
        CancellationToken cancellationToken = default
    ) =>
        EnsureOccurrencesAsync(
            db,
            timeZoneService,
            loanOutProvider,
            [(locationId, userIds, new UtcDateRange(startAtUtc, endAtUtc ?? startAtUtc))],
            cancellationToken
        );

    public static Task EnsureEntriesCanScheduleAsync(
        UnifiedDbContext db,
        ITimeZoneService timeZoneService,
        ISchedulingCalendarEventDataProvider<SchedulingCalendarEventResponse>? loanOutProvider,
        IReadOnlyCollection<ShiftEntry> entries,
        CancellationToken cancellationToken = default
    ) =>
        EnsureOccurrencesAsync(
            db,
            timeZoneService,
            loanOutProvider,
            entries
                .Where(entry =>
                    entry.Event is not null && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                )
                .Select(entry =>
                    (
                        entry.Event!.LocationId,
                        UserIds: (IReadOnlyCollection<Guid>)entry.Users.Select(user => user.UserId).ToList(),
                        Range: new UtcDateRange(entry.Event.StartAtUtc, entry.Event.EndAtUtc ?? entry.Event.StartAtUtc)
                    )
                )
                .ToList(),
            cancellationToken
        );

    private static async Task EnsureOccurrencesAsync(
        UnifiedDbContext db,
        ITimeZoneService timeZoneService,
        ISchedulingCalendarEventDataProvider<SchedulingCalendarEventResponse>? loanOutProvider,
        IReadOnlyCollection<(int? LocationId, IReadOnlyCollection<Guid> UserIds, UtcDateRange Range)> occurrences,
        CancellationToken cancellationToken
    )
    {
        if (loanOutProvider is null)
            return;
        var candidates = occurrences.Where(item => item.LocationId.HasValue && item.UserIds.Count > 0).ToList();
        if (candidates.Count == 0)
            return;
        var userIds = candidates.SelectMany(item => item.UserIds).Distinct().ToList();
        var homeLocations = await db
            .Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.HomeLocationId, cancellationToken);
        var locationIds = candidates.Select(item => item.LocationId!.Value).Distinct().ToList();
        var unknownHomeUsers = userIds.Where(id => !homeLocations.GetValueOrDefault(id).HasValue).ToList();
        // A previous loan-out identifies borrowed employees whose home location is unset.
        var previousDestinations =
            unknownHomeUsers.Count == 0
                ? []
                : await db
                    .LoanOutEntryUsers.AsNoTracking()
                    .Where(user =>
                        unknownHomeUsers.Contains(user.UserId)
                        && user.LoanOutEntry != null
                        && user.LoanOutEntry.Event != null
                    )
                    .Select(user => new
                    {
                        user.UserId,
                        user.LoanOutEntry!.LoanOutLocationId,
                        SourceLocationId = user.LoanOutEntry.Event!.LocationId,
                    })
                    .Distinct()
                    .ToListAsync(cancellationToken);
        var borrowedWithoutHome = previousDestinations
            .Select(item => (item.UserId, item.LoanOutLocationId))
            .ToHashSet();
        var previousBorrowers = previousDestinations.Select(item => item.UserId).ToHashSet();
        var knownSourceLocations = previousDestinations
            .Where(item => item.SourceLocationId.HasValue)
            .Select(item => (item.UserId, item.SourceLocationId!.Value))
            .ToHashSet();
        var required = candidates
            .Select(item =>
                (
                    LocationId: item.LocationId!.Value,
                    UserIds: item.UserIds.Where(id =>
                            homeLocations.GetValueOrDefault(id) is int home
                                ? home != item.LocationId.Value
                                : borrowedWithoutHome.Contains((id, item.LocationId.Value))
                                    || previousBorrowers.Contains(id)
                                        && !knownSourceLocations.Contains((id, item.LocationId.Value))
                        )
                        .ToList(),
                    item.Range
                )
            )
            .Where(item => item.UserIds.Count > 0)
            .ToList();
        if (required.Count == 0)
            return;
        var requiredUserIds = required.SelectMany(item => item.UserIds).Distinct().ToHashSet();
        var zoneLocationIds = locationIds.Concat(homeLocations.Values.OfType<int>()).Distinct().ToList();
        var locationZones = await db
            .Locations.AsNoTracking()
            .Where(location => zoneLocationIds.Contains(location.Id))
            .ToDictionaryAsync(location => location.Id, location => location.Timezone, cancellationToken);
        var days = required
            .SelectMany(item =>
                ShiftLoanOutGuard
                    .GetLocalDates(
                        item.Range,
                        timeZoneService,
                        timeZoneService.ResolveRequired(locationZones[item.LocationId])
                    )
                    .Select(date => (item.LocationId, item.UserIds, Date: date))
            )
            .ToList();
        var ranges = days.Select(item =>
                timeZoneService.ConvertInclusiveLocalDateRangeToUtcRange(
                    item.Date,
                    item.Date,
                    timeZoneService.ResolveRequired(locationZones[item.LocationId])
                )
            )
            .ToList();
        // Include loan-outs expressed in a different source time zone for the same calendar dates.
        var loanOuts = await loanOutProvider.GetDataAsync(
            new SchedulingCalendarEventQueryContext
            {
                StartAtUtc = ranges.Min(range => range.StartAtUtc).AddDays(-2),
                EndAtUtc = ranges.Max(range => range.EndAtUtc).AddDays(2),
                UserIds = requiredUserIds.ToList(),
            },
            cancellationToken
        );
        var active = loanOuts
            .Where(item =>
                item.LoanOutEntryId.HasValue
                && item.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                && item.LoanOutLocationId.HasValue
                && locationIds.Contains(item.LoanOutLocationId.Value)
            )
            .ToList();
        var missingSourceZones = active
            .Where(item => string.IsNullOrWhiteSpace(item.TimeZoneId) && item.LocationId.HasValue)
            .Select(item => item.LocationId!.Value)
            .Except(locationZones.Keys)
            .ToList();
        if (missingSourceZones.Count > 0)
            foreach (
                var location in await db
                    .Locations.AsNoTracking()
                    .Where(item => missingSourceZones.Contains(item.Id))
                    .ToListAsync(cancellationToken)
            )
                locationZones[location.Id] = location.Timezone;
        var coverage = new List<(Guid UserId, int LocationId, DateOnly Start, DateOnly End)>();
        foreach (var loanOut in active)
        {
            foreach (var userId in loanOut.UserIds.Where(requiredUserIds.Contains))
            {
                var sourceLocation = loanOut.LocationId ?? homeLocations.GetValueOrDefault(userId);
                var sourceZone = timeZoneService.ResolveOrUtc(
                    string.IsNullOrWhiteSpace(loanOut.TimeZoneId)
                        ? sourceLocation.HasValue
                            ? locationZones.GetValueOrDefault(sourceLocation.Value)
                            : null
                        : loanOut.TimeZoneId
                );
                var coveredDates = ShiftLoanOutGuard
                    .GetLocalDates(
                        new UtcDateRange(loanOut.Start, loanOut.End ?? loanOut.Start),
                        timeZoneService,
                        sourceZone
                    )
                    .ToList();
                coverage.Add((userId, loanOut.LoanOutLocationId!.Value, coveredDates[0], coveredDates[^1]));
            }
        }
        var coverageByUserAndLocation = coverage.ToLookup(item => (item.UserId, item.LocationId));
        foreach (var day in days)
            if (
                day.UserIds.Any(userId =>
                    !coverageByUserAndLocation[(userId, day.LocationId)]
                        .Any(item => item.Start <= day.Date && day.Date <= item.End)
                )
            )
                throw new ConflictValidationException(
                    new Dictionary<string, string[]>
                    {
                        ["LoanOut"] =
                        [
                            $"One or more selected employees are not loaned out to this location on {day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}. The shift cannot be created or updated.",
                        ],
                    }
                );
    }
}
