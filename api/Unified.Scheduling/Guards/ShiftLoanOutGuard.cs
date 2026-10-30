using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Services;
using Unified.Common.Scheduling;
using Unified.Common.Scheduling.Models;
using Unified.Common.Time;
using Unified.Common.Validation;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Guards;

public static class ShiftLoanOutGuard
{
    public static Task EnsureCanCreateAsync(
        UnifiedDbContext db,
        ITimeZoneService timeZoneService,
        ISchedulingCalendarEventDataProvider<SchedulingCalendarEventResponse>? schedulingCalendarDataProvider,
        int? locationId,
        IReadOnlyCollection<Guid> userIds,
        DateTimeOffset startAtUtc,
        DateTimeOffset? endAtUtc,
        CancellationToken cancellationToken = default
    ) =>
        EnsureRangesAsync(
            db,
            timeZoneService,
            schedulingCalendarDataProvider,
            locationId,
            userIds,
            [new UtcDateRange(startAtUtc, endAtUtc ?? startAtUtc)],
            false,
            cancellationToken
        );

    public static Task EnsureSeriesCanCreateAsync(
        UnifiedDbContext db,
        ITimeZoneService timeZoneService,
        ISchedulingCalendarEventDataProvider<SchedulingCalendarEventResponse>? schedulingCalendarDataProvider,
        int? locationId,
        IReadOnlyCollection<Guid> userIds,
        DateTimeOffset startAtUtc,
        DateTimeOffset? endAtUtc,
        string? timeZoneId,
        string recurrenceRule,
        IRecurrenceExpander recurrenceExpander,
        CancellationToken cancellationToken = default
    )
    {
        if (schedulingCalendarDataProvider is null)
            return Task.CompletedTask;
        var occurrences = recurrenceExpander.ExpandAllBounded(
            new EventSeries
            {
                StartAtUtc = startAtUtc,
                EndAtUtc = endAtUtc,
                TimeZoneId = timeZoneId,
                RecurrenceRule = recurrenceRule,
            },
            400
        );
        return EnsureRangesAsync(
            db,
            timeZoneService,
            schedulingCalendarDataProvider,
            locationId,
            userIds,
            occurrences
                .Select(occurrence => new UtcDateRange(
                    occurrence.StartAtUtc,
                    occurrence.EndAtUtc ?? occurrence.StartAtUtc
                ))
                .ToList(),
            true,
            cancellationToken
        );
    }

    private static async Task EnsureRangesAsync(
        UnifiedDbContext db,
        ITimeZoneService timeZoneService,
        ISchedulingCalendarEventDataProvider<SchedulingCalendarEventResponse>? schedulingCalendarDataProvider,
        int? locationId,
        IReadOnlyCollection<Guid> userIds,
        IReadOnlyCollection<UtcDateRange> occurrences,
        bool recurring,
        CancellationToken cancellationToken = default
    )
    {
        if (
            schedulingCalendarDataProvider is null
            || !locationId.HasValue
            || userIds.Count == 0
            || occurrences.Count == 0
        )
            return;

        // var selectedUsers = await db.Users.AsNoTracking()
        //     .Where(user => userIds.Contains(user.Id))
        //     .Select(user => new { user.Id, user.FirstName, user.LastName })
        //     .ToListAsync(cancellationToken);

        var timeZoneId = await db
            .Locations.AsNoTracking()
            .Where(location => location.Id == locationId.Value)
            .Select(location => location.Timezone)
            .SingleAsync(cancellationToken);
        var timeZone = timeZoneService.ResolveRequired(timeZoneId);
        var dates = occurrences
            .SelectMany(occurrence => GetLocalDates(occurrence, timeZoneService, timeZone))
            .Distinct()
            .Order()
            .ToList();
        var ranges = dates
            .Select(date =>
                (Date: date, Range: timeZoneService.ConvertInclusiveLocalDateRangeToUtcRange(date, date, timeZone))
            )
            .ToList();
        var events = await schedulingCalendarDataProvider.GetDataAsync(
            new SchedulingCalendarEventQueryContext
            {
                StartAtUtc = ranges[0].Range.StartAtUtc,
                EndAtUtc = ranges[^1].Range.EndAtUtc,
                UserIds = userIds,
            },
            cancellationToken
        );

        var homeLocations = events.Any(item => item.LocationId is null)
            ? await db
                .Users.AsNoTracking()
                .Where(user => userIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, user => user.HomeLocationId, cancellationToken)
            : new Dictionary<Guid, int?>();
        foreach (var (_, range) in ranges)
        {
            if (
                events.Any(item =>
                    item.LoanOutEntryId.HasValue
                    && item.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                    && item.LoanOutLocationId != locationId
                    && item.UserIds.Any(userId =>
                        userIds.Contains(userId)
                        && (item.LocationId ?? homeLocations.GetValueOrDefault(userId)) == locationId
                    )
                    && item.Start < range.EndAtUtc
                    && (item.End.HasValue ? item.End.Value > range.StartAtUtc : item.Start >= range.StartAtUtc)
                )
            )
            {
                var message = recurring
                    ? "One or more selected employees are loaned out during the selected dates. A shift cannot be created at their home location during that period."
                    : "Employee is loaned out. A shift cannot be created .";
                throw new ConflictValidationException(new Dictionary<string, string[]> { ["LoanOut"] = [message] });
            }
        }
    }

    internal static IEnumerable<DateOnly> GetLocalDates(
        UtcDateRange occurrence,
        ITimeZoneService timeZoneService,
        TimeZoneInfo timeZone
    )
    {
        if (occurrence.EndAtUtc < occurrence.StartAtUtc)
            throw new InvalidOperationException("The shift end must not precede its start.");
        var firstDate = DateOnly.FromDateTime(timeZoneService.ToLocalUnspecified(occurrence.StartAtUtc, timeZone));
        var lastInstant =
            occurrence.EndAtUtc > occurrence.StartAtUtc ? occurrence.EndAtUtc.AddTicks(-1) : occurrence.StartAtUtc;
        var lastDate = DateOnly.FromDateTime(timeZoneService.ToLocalUnspecified(lastInstant, timeZone));
        return Enumerable.Range(0, lastDate.DayNumber - firstDate.DayNumber + 1).Select(firstDate.AddDays);
    }
}
