using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Leave.FeatureFlags;
using Unified.Scheduling.Models;
using Unified.Scheduling.Options;
using Unified.Scheduling.Validators;

namespace Unified.Scheduling.Services;

public sealed class WorkingHoursService(
    ILogger<WorkingHoursService> logger,
    UnifiedDbContext db,
    IOptions<WorkingHoursOptions> options,
    ITimeZoneService timeZoneService,
    IOptions<LeaveFeatureFlags> leaveOptions,
    ICalendarTimeZoneResolver timeZoneResolver
) : IWorkingHoursService
{
    public async Task<IReadOnlyCollection<WorkingHoursResult>> QueryAsync(
        WorkingHoursQuery query,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation(
            "Querying for working hours for interval {StartDate} - {EndDate}, for {LocationCount} locations and {UserCount} users.",
            query.StartDate,
            query.EndDate,
            query.ShiftLocationIds?.Count ?? 0,
            query.UserIds?.Count ?? 0
        );

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Exact query: {@Query}", query);
        }

        new WorkingHoursQueryValidator(options).ValidateAndThrow(query);

        // StartAtUtc is stored in UTC, while the requested dates represent
        // business/calendar dates. Fetch a slightly wider UTC window and
        // apply the exact business-date filter after timezone conversion.
        var startDate = query.StartDate == DateOnly.MinValue ? query.StartDate : query.StartDate.AddDays(-1);
        var utcStart = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        var utcEndExclusive =
            query.EndDate > DateOnly.MaxValue.AddDays(-2)
                ? DateTimeOffset.MaxValue
                : new DateTimeOffset(query.EndDate.AddDays(2).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        var shiftsQuery = db
            .ShiftEntries.AsNoTracking()
            .Where(x =>
                x.Event != null
                && x.Event.EndAtUtc != null
                && x.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                && x.Event.StartAtUtc >= utcStart
                && x.Event.StartAtUtc < utcEndExclusive
            );

        if (query.ShiftLocationIds is { Count: > 0 })
        {
            shiftsQuery = shiftsQuery.Where(x =>
                x.Event!.LocationId.HasValue && query.ShiftLocationIds.Contains(x.Event.LocationId.Value)
            );
        }

        // A ShiftEntry can contain multiple users, so flatten into the
        // actual working-hours calculation unit: ShiftEntry + User.
        var shiftUsersQuery = shiftsQuery.SelectMany(
            shift => shift.Users,
            (shift, user) => new { Shift = shift, User = user }
        );

        if (query.UserIds is { Count: > 0 })
        {
            shiftUsersQuery = shiftUsersQuery.Where(x => query.UserIds.Contains(x.User.UserId));
        }

        var shifts = await shiftUsersQuery
            .Select(x => new ShiftWorkHoursData(
                x.Shift.Id,
                x.User.UserId,
                x.Shift.Event!.StartAtUtc,
                x.Shift.Event.EndAtUtc!.Value,
                x.Shift.Event.TimeZoneId,
                x.Shift.Event.Location != null ? x.Shift.Event.Location.Timezone : null,
                x.Shift.LunchAvailableMinutes,
                x.Shift.WorkedLunchMinutes
            ))
            .ToListAsync(cancellationToken);

        // Apply the exact local/business-date filter.
        shifts = shifts
            .Where(x =>
            {
                var date = GetBusinessDate(x);

                return date >= query.StartDate && date <= query.EndDate;
            })
            .ToList();

        if (shifts.Count == 0)
        {
            return [];
        }

        var shiftEntryIds = shifts.Select(x => x.ShiftEntryId).Distinct().ToArray();

        // Assignment ownership is represented by ShiftAssignmentEntry.Users.
        // AssignmentEntry provides the assignment Event/timing.
        var assignmentUsersQuery = db
            .ShiftAssignmentEntries.AsNoTracking()
            .Where(x =>
                shiftEntryIds.Contains(x.ShiftEntryId)
                && x.AssignmentEntry != null
                && x.AssignmentEntry.Event != null
                && x.AssignmentEntry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                && x.AssignmentEntry.Event.EndAtUtc != null
            )
            .SelectMany(link => link.Users, (link, user) => new { Link = link, User = user });

        if (query.UserIds is { Count: > 0 })
        {
            assignmentUsersQuery = assignmentUsersQuery.Where(x => query.UserIds.Contains(x.User.UserId));
        }

        var assignments = await assignmentUsersQuery
            .Select(x => new AssignmentWorkHoursData(
                x.Link.ShiftEntryId,
                x.User.UserId,
                x.Link.AssignmentEntry!.Event!.StartAtUtc,
                x.Link.AssignmentEntry.Event.EndAtUtc!.Value
            ))
            .ToListAsync(cancellationToken);

        var assignmentsByShiftUser = assignments
            .GroupBy(x => (x.ShiftEntryId, x.UserId))
            .ToDictionary(x => x.Key, x => (IReadOnlyCollection<AssignmentWorkHoursData>)x.ToList());

        var leaves = new List<LeaveWorkHoursData>();
        if (leaveOptions.Value.Enabled)
        {
            var userIds = shifts.Select(x => x.UserId).Distinct().ToArray();
            var start = shifts.Min(x => x.Start);
            var end = shifts.Max(x => x.End);
            leaves = await db
                .LeaveEntries.AsNoTracking()
                .Where(x =>
                    x.Event != null
                    && x.LeaveType != null
                    && x.Event.EndAtUtc > x.Event.StartAtUtc
                    && x.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                    && x.Event.StartAtUtc < end
                    && x.Event.EndAtUtc > start
                )
                .SelectMany(entry => entry.Users, (entry, user) => new { Entry = entry, User = user })
                .Where(x => userIds.Contains(x.User.UserId))
                .Select(x => new LeaveWorkHoursData(
                    x.User.UserId,
                    x.Entry.Event!.StartAtUtc,
                    x.Entry.Event.EndAtUtc!.Value,
                    x.Entry.LeaveType!.IsPaid
                ))
                .ToListAsync(cancellationToken);
        }

        var leavesByUser = leaves.ToLookup(x => x.UserId);
        return shifts
            .Select(shift =>
            {
                assignmentsByShiftUser.TryGetValue((shift.ShiftEntryId, shift.UserId), out var shiftAssignments);

                return Calculate(shift, shiftAssignments ?? [], leavesByUser[shift.UserId]);
            })
            .ToList();
    }

    private WorkingHoursResult Calculate(
        ShiftWorkHoursData shift,
        IReadOnlyCollection<AssignmentWorkHoursData> assignments,
        IEnumerable<LeaveWorkHoursData> leaves
    )
    {
        var paidOutsideShiftMinutes = CalculatePaidOutsideShiftMinutes(shift, assignments);
        if (
            shift.LunchAvailableMinutes is not int lunchAvailableMinutes
            || shift.WorkedLunchMinutes is not int workedLunchMinutes
        )
        {
            return new WorkingHoursResult
            {
                ShiftEntryId = shift.ShiftEntryId,
                UserId = shift.UserId,
                Date = GetBusinessDate(shift),
                LunchAvailableMinutes = shift.LunchAvailableMinutes,
                WorkedLunchMinutes = null,
                PaidOutsideShiftMinutes = paidOutsideShiftMinutes,
                PaidShiftMinutes = null,
                WorkedMinutes = null,
                PaidLeaveMinutes = null,
                UnpaidLeaveMinutes = null,
                CreditedMinutes = null,
                OvertimeMinutes = null,
            };
        }

        var regularMinutes = Math.Max(0, GetDurationMinutes(shift.Start, shift.End) - lunchAvailableMinutes);
        var (paidLeaveMinutes, unpaidLeaveMinutes) = CalculateLeaveMinutes(shift, leaves, regularMinutes);
        var remainingRegularMinutes = regularMinutes - paidLeaveMinutes - unpaidLeaveMinutes;
        var effectiveWorkedLunchMinutes = Math.Min(Math.Max(0, workedLunchMinutes), remainingRegularMinutes);
        var paidShiftMinutes = regularMinutes + effectiveWorkedLunchMinutes;

        var inShiftWorkedMinutes = remainingRegularMinutes + effectiveWorkedLunchMinutes;
        var workedMinutes = inShiftWorkedMinutes + paidOutsideShiftMinutes;
        var creditedMinutes = workedMinutes + paidLeaveMinutes;

        return new WorkingHoursResult
        {
            ShiftEntryId = shift.ShiftEntryId,
            UserId = shift.UserId,
            Date = GetBusinessDate(shift),
            PaidShiftMinutes = paidShiftMinutes,
            LunchAvailableMinutes = shift.LunchAvailableMinutes,
            WorkedLunchMinutes = effectiveWorkedLunchMinutes,
            PaidOutsideShiftMinutes = paidOutsideShiftMinutes,
            WorkedMinutes = workedMinutes,
            PaidLeaveMinutes = paidLeaveMinutes,
            UnpaidLeaveMinutes = unpaidLeaveMinutes,
            CreditedMinutes = creditedMinutes,
            OvertimeMinutes = Math.Max(0, creditedMinutes - options.Value.FullWorkingDayMinutes),
        };
    }

    private static (int Paid, int Unpaid) CalculateLeaveMinutes(
        ShiftWorkHoursData shift,
        IEnumerable<LeaveWorkHoursData> leaves,
        int regularMinutes
    )
    {
        var clipped = leaves
            .Where(x => x.Start < shift.End && x.End > shift.Start)
            .Select(x => new
            {
                Interval = new TimeInterval(Max(x.Start, shift.Start), Min(x.End, shift.End)),
                x.IsPaid,
            })
            .ToList();
        var total = MergeIntervals(clipped.Select(x => x.Interval)).Sum(x => GetDurationMinutes(x.Start, x.End));
        var unpaid = MergeIntervals(clipped.Where(x => !x.IsPaid).Select(x => x.Interval))
            .Sum(x => GetDurationMinutes(x.Start, x.End));

        // Conflicting classifications are conservatively unpaid. Unioning all leave first
        // ensures that an overlapping minute is never credited twice.
        var unpaidMinutes = Math.Min(regularMinutes, unpaid);
        // Lunch has no recorded interval. Partial leave is treated as paid working-time
        // coverage; the combined cap excludes unpaid lunch for full-shift leave.
        var paidMinutes = Math.Min(regularMinutes - unpaidMinutes, total - unpaid);
        return (paidMinutes, unpaidMinutes);
    }

    /// <summary>
    /// Safely merge all overlapping assignment intervals and sum all minutes that fall outside of those intervals and the shift interval.
    /// </summary>
    /// <param name="shift"></param>
    /// <param name="assignments"></param>
    /// <returns></returns>
    private static int CalculatePaidOutsideShiftMinutes(
        ShiftWorkHoursData shift,
        IReadOnlyCollection<AssignmentWorkHoursData> assignments
    )
    {
        if (assignments.Count == 0)
        {
            return 0;
        }

        var mergedAssignments = MergeIntervals(assignments.Select(x => new TimeInterval(x.Start, x.End)));

        var shiftInterval = new TimeInterval(shift.Start, shift.End);

        return mergedAssignments.Sum(assignment => CalculateMinutesOutsideShift(assignment, shiftInterval));
    }

    /// <summary>
    /// Calculate all assignment minutes that fall out of a user's shift (before/after).
    /// </summary>
    /// <param name="assignment"></param>
    /// <param name="shift"></param>
    /// <returns></returns>
    private static int CalculateMinutesOutsideShift(TimeInterval assignment, TimeInterval shift)
    {
        var minutes = 0;

        if (assignment.Start < shift.Start)
        {
            var end = Min(assignment.End, shift.Start);

            if (end > assignment.Start)
            {
                minutes += GetDurationMinutes(assignment.Start, end);
            }
        }

        if (assignment.End > shift.End)
        {
            var start = Max(assignment.Start, shift.End);

            if (assignment.End > start)
            {
                minutes += GetDurationMinutes(start, assignment.End);
            }
        }

        return minutes;
    }

    /// <summary>
    /// Combine intervals of overlapping time intervals to avoid double-counting.
    /// </summary>
    /// <param name="intervals"></param>
    /// <returns></returns>
    private static IReadOnlyCollection<TimeInterval> MergeIntervals(IEnumerable<TimeInterval> intervals)
    {
        var ordered = intervals.Where(x => x.End > x.Start).OrderBy(x => x.Start).ThenBy(x => x.End).ToList();

        if (ordered.Count <= 1)
        {
            return ordered;
        }

        var result = new List<TimeInterval>();
        var current = ordered[0];

        foreach (var next in ordered.Skip(1))
        {
            if (next.Start <= current.End)
            {
                current = new TimeInterval(current.Start, Max(current.End, next.End));

                continue;
            }

            result.Add(current);
            current = next;
        }

        result.Add(current);

        return result;
    }

    private DateOnly GetBusinessDate(ShiftWorkHoursData shift)
    {
        var timeZone = timeZoneResolver.Resolve(shift.TimeZoneId, shift.LocationTimeZoneId);
        return DateOnly.FromDateTime(timeZoneService.ToLocalUnspecified(shift.Start, timeZone));
    }

    private static int GetDurationMinutes(DateTimeOffset start, DateTimeOffset end)
    {
        return (int)(end - start).TotalMinutes;
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right)
    {
        return left <= right ? left : right;
    }

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right)
    {
        return left >= right ? left : right;
    }

    private readonly record struct TimeInterval(DateTimeOffset Start, DateTimeOffset End);

    private sealed record ShiftWorkHoursData(
        int ShiftEntryId,
        Guid UserId,
        DateTimeOffset Start,
        DateTimeOffset End,
        string? TimeZoneId,
        string? LocationTimeZoneId,
        int? LunchAvailableMinutes,
        int? WorkedLunchMinutes
    );

    private sealed record AssignmentWorkHoursData(
        int ShiftEntryId,
        Guid UserId,
        DateTimeOffset Start,
        DateTimeOffset End
    );

    private sealed record LeaveWorkHoursData(Guid UserId, DateTimeOffset Start, DateTimeOffset End, bool IsPaid);
}
