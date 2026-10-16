using Unified.Common.Time;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;

namespace Unified.Scheduling.Services;

internal static class SchedulingEntryQueryExtensions
{
    public static IQueryable<ShiftEntry> InSchedulingRange(this IQueryable<ShiftEntry> query, UtcDateRange range) =>
        query.Where(entry =>
            entry.Event != null
            && entry.Event.SourceModule == SchedulingConstants.SourceModule
            && entry.Event.EventTypeCode == SchedulingConstants.ShiftEventTypeCode
            && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
            && entry.Event.StartAtUtc < range.EndAtUtc
            && (entry.Event.EndAtUtc ?? entry.Event.StartAtUtc) > range.StartAtUtc
        );

    public static IQueryable<AssignmentEntry> InSchedulingRange(
        this IQueryable<AssignmentEntry> query,
        UtcDateRange range
    ) =>
        query.Where(entry =>
            entry.Event != null
            && entry.Event.SourceModule == SchedulingConstants.SourceModule
            && entry.Event.EventTypeCode == SchedulingConstants.AssignmentEventTypeCode
            && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
            && entry.Event.StartAtUtc < range.EndAtUtc
            && (entry.Event.EndAtUtc ?? entry.Event.StartAtUtc) > range.StartAtUtc
        );
}
