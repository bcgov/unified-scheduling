using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Scheduling.Models;
using Unified.Scheduling.Options;

namespace Unified.Scheduling.Services;

public sealed class WorkingHoursService(
    ILogger<WorkingHoursService> logger,
    UnifiedDbContext db,
    IOptions<WorkingHoursOptions> options,
    ITimeZoneService timeZoneService) : IWorkingHoursService
{
    public async Task<IReadOnlyCollection<WorkingHoursResult>> QueryAsync(
        WorkingHoursQuery query,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Querying for working hours for interval {StartDate} - {EndDate}, for {LocationCount} locations and {UserCount} users.",
            query.StartDate,
            query.EndDate,
            query.ShiftLocationIds?.Count ?? 0,
            query.UserIds?.Count ?? 0);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Exact query: {@Query}", query);
        }

        ValidateQuery(query);

        // StartAtUtc is stored in UTC, while the requested dates represent
        // business/calendar dates. Fetch a slightly wider UTC window and
        // apply the exact business-date filter after timezone conversion.
        var utcStart = new DateTimeOffset(
            query.StartDate
                .AddDays(-1)
                .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        var utcEndExclusive = new DateTimeOffset(
            query.EndDate
                .AddDays(2)
                .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        var shiftsQuery = db.ShiftEntries
            .AsNoTracking()
            .Where(x =>
                x.Event != null &&
                x.Event.EndAtUtc != null &&
                x.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled &&
                x.Event.StartAtUtc >= utcStart &&
                x.Event.StartAtUtc < utcEndExclusive);

        if (query.ShiftLocationIds is { Count: > 0 })
        {
            shiftsQuery = shiftsQuery.Where(x =>
                x.Event!.LocationId.HasValue &&
                query.ShiftLocationIds.Contains(x.Event.LocationId.Value));
        }

        // A ShiftEntry can contain multiple users, so flatten into the
        // actual working-hours calculation unit: ShiftEntry + User.
        var shiftUsersQuery = shiftsQuery
            .SelectMany(
                shift => shift.Users,
                (shift, user) => new ShiftWorkHoursData(
                    shift.Id,
                    user.UserId,
                    shift.Event!.StartAtUtc,
                    shift.Event.EndAtUtc!.Value,
                    shift.Event.TimeZoneId,
                    shift.LunchAvailableMinutes,
                    shift.WorkedLunchMinutes));

        if (query.UserIds is { Count: > 0 })
        {
            shiftUsersQuery = shiftUsersQuery.Where(
                x => query.UserIds.Contains(x.UserId));
        }

        var shifts = await shiftUsersQuery
            .ToListAsync(cancellationToken);

        // Apply the exact local/business-date filter.
        shifts = shifts
            .Where(x =>
            {
                var date = GetBusinessDate(
                    x.Start,
                    x.TimeZoneId);

                return date >= query.StartDate &&
                       date <= query.EndDate;
            })
            .ToList();

        if (shifts.Count == 0)
        {
            return [];
        }

        var shiftEntryIds = shifts
            .Select(x => x.ShiftEntryId)
            .Distinct()
            .ToArray();

        // Assignment ownership is represented by ShiftAssignmentEntry.Users.
        // AssignmentEntry provides the assignment Event/timing.
        var assignmentUsersQuery = db.ShiftAssignmentEntries
            .AsNoTracking()
            .Where(x =>
                shiftEntryIds.Contains(x.ShiftEntryId) &&
                x.AssignmentEntry != null &&
                x.AssignmentEntry.Event != null &&
                x.AssignmentEntry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled &&
                x.AssignmentEntry.Event.EndAtUtc != null)
            .SelectMany(
                link => link.Users,
                (link, user) => new AssignmentWorkHoursData(
                    link.ShiftEntryId,
                    user.UserId,
                    link.AssignmentEntry!.Event!.StartAtUtc,
                    link.AssignmentEntry.Event.EndAtUtc!.Value));

        if (query.UserIds is { Count: > 0 })
        {
            assignmentUsersQuery = assignmentUsersQuery.Where(
                x => query.UserIds.Contains(x.UserId));
        }

        var assignments = await assignmentUsersQuery
            .ToListAsync(cancellationToken);

        var assignmentsByShiftUser = assignments
            .GroupBy(x => (x.ShiftEntryId, x.UserId))
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyCollection<AssignmentWorkHoursData>)x.ToList());

        return shifts
            .Select(shift =>
            {
                assignmentsByShiftUser.TryGetValue(
                    (shift.ShiftEntryId, shift.UserId),
                    out var shiftAssignments);

                return Calculate(
                    shift,
                    shiftAssignments ?? []);
            })
            .ToList();
    }

    private WorkingHoursResult Calculate(
        ShiftWorkHoursData shift,
        IReadOnlyCollection<AssignmentWorkHoursData> assignments)
    {
        var paidShiftMinutes =
            CalculatePaidShiftMinutes(shift);

        var paidOutsideShiftMinutes =
            CalculatePaidOutsideShiftMinutes(
                shift,
                assignments);

        var creditedMinutes =
            paidShiftMinutes +
            paidOutsideShiftMinutes;

        return new WorkingHoursResult
        {
            UserId = shift.UserId,
            Date = GetBusinessDate(
                shift.Start,
                shift.TimeZoneId),
            PaidShiftMinutes = paidShiftMinutes,
            WorkedLunchMinutes = shift.WorkedLunchMinutes,
            PaidOutsideShiftMinutes = paidOutsideShiftMinutes,
            CreditedMinutes = creditedMinutes,
            OvertimeMinutes =
                CalculateOvertimeMinutes(creditedMinutes)
        };
    }


    /// <summary>
    /// Get the worked minutes within the shift. Which is the shift minutes less the lunch taken/worked (note that worked lunch is ALWAYS less than Lunch available) 
    /// </summary>
    /// <param name="shift"></param>
    /// <returns></returns>
    private static int CalculatePaidShiftMinutes(
        ShiftWorkHoursData shift)
    {
        var shiftMinutes =
            GetDurationMinutes(
                shift.Start,
                shift.End);

        return shiftMinutes
            - shift.LunchAvailableMinutes
            + shift.WorkedLunchMinutes;
    }

    /// <summary>
    /// Safely merge all overlapping assignment intervals and sum all minutes that fall outside of those intervals and the shift interval.
    /// </summary>
    /// <param name="shift"></param>
    /// <param name="assignments"></param>
    /// <returns></returns>
    private static int CalculatePaidOutsideShiftMinutes(
        ShiftWorkHoursData shift,
        IReadOnlyCollection<AssignmentWorkHoursData> assignments)
    {
        if (assignments.Count == 0)
        {
            return 0;
        }

        var mergedAssignments = MergeIntervals(
            assignments.Select(x =>
                new TimeInterval(
                    x.Start,
                    x.End)));

        var shiftInterval = new TimeInterval(
            shift.Start,
            shift.End);

        return mergedAssignments.Sum(
            assignment =>
                CalculateMinutesOutsideShift(
                    assignment,
                    shiftInterval));
    }

    /// <summary>
    /// Calculate all assignment minutes that fall out of a user's shift (before/after).
    /// </summary>
    /// <param name="assignment"></param>
    /// <param name="shift"></param>
    /// <returns></returns>
    private static int CalculateMinutesOutsideShift(
        TimeInterval assignment,
        TimeInterval shift)
    {
        var minutes = 0;

        if (assignment.Start < shift.Start)
        {
            var end = Min(
                assignment.End,
                shift.Start);

            if (end > assignment.Start)
            {
                minutes += GetDurationMinutes(
                    assignment.Start,
                    end);
            }
        }

        if (assignment.End > shift.End)
        {
            var start = Max(
                assignment.Start,
                shift.End);

            if (assignment.End > start)
            {
                minutes += GetDurationMinutes(
                    start,
                    assignment.End);
            }
        }

        return minutes;
    }

    private int CalculateOvertimeMinutes(
        int creditedMinutes)
    {
        return Math.Max(
            0,
            creditedMinutes -
            options.Value.FullWorkingDayMinutes);
    }

    /// <summary>
    /// Combine intervals of overlapping time intervals to avoid double-counting.
    /// </summary>
    /// <param name="intervals"></param>
    /// <returns></returns>
    private static IReadOnlyCollection<TimeInterval> MergeIntervals(
        IEnumerable<TimeInterval> intervals)
    {
        var ordered = intervals
            .OrderBy(x => x.Start)
            .ThenBy(x => x.End)
            .ToList();

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
                current = new TimeInterval(
                    current.Start,
                    Max(current.End, next.End));

                continue;
            }

            result.Add(current);
            current = next;
        }

        result.Add(current);

        return result;
    }

    private void ValidateQuery(
        WorkingHoursQuery query)
    {
        if (query.EndDate < query.StartDate)
        {
            throw new ArgumentException(
                "EndDate must be greater than or equal to StartDate.");
        }

        var rangeDays =
            query.EndDate.DayNumber -
            query.StartDate.DayNumber +
            1;

        if (rangeDays > options.Value.MaxQueryRangeDays)
        {
            throw new ArgumentException(
                $"Working-hours queries cannot exceed " +
                $"{options.Value.MaxQueryRangeDays} days.");
        }
    }

    private DateOnly GetBusinessDate(
    DateTimeOffset value,
    string? timeZoneId)
    {
        return DateOnly.FromDateTime(
            timeZoneService.ToTimeZone(value, timeZoneId).DateTime);
    }

    private static int GetDurationMinutes(
        DateTimeOffset start,
        DateTimeOffset end)
    {
        return (int)(end - start).TotalMinutes;
    }

    private static DateTimeOffset Min(
        DateTimeOffset left,
        DateTimeOffset right)
    {
        return left <= right
            ? left
            : right;
    }

    private static DateTimeOffset Max(
        DateTimeOffset left,
        DateTimeOffset right)
    {
        return left >= right
            ? left
            : right;
    }

    private readonly record struct TimeInterval(
        DateTimeOffset Start,
        DateTimeOffset End);

    private sealed record ShiftWorkHoursData(
        int ShiftEntryId,
        Guid UserId,
        DateTimeOffset Start,
        DateTimeOffset End,
        string? TimeZoneId,
        int LunchAvailableMinutes,
        int WorkedLunchMinutes);

    private sealed record AssignmentWorkHoursData(
        int ShiftEntryId,
        Guid UserId,
        DateTimeOffset Start,
        DateTimeOffset End);
}
