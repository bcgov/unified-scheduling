using Unified.Db.Models.Calendar;

namespace Unified.Scheduling.Services;

internal static class ShiftGuards
{
    public static void EnsureShiftSeriesIsDraft(EventSeries eventSeries)
    {
        if (eventSeries.StatusTypeCode != CalendarEventStatusTypeCodes.Draft)
            throw new InvalidOperationException("Shift series must be in draft status to allow edits.");
    }

    public static void EnsureShiftEventSeriesType(EventSeries eventSeries)
    {
        if (eventSeries.EventTypeCode != SchedulingConstants.ShiftEventTypeCode)
            throw new InvalidOperationException($"Event series {eventSeries.Id} is not a shift event series.");
    }

    public static void EnsureShiftEventType(Event eventEntity)
    {
        if (eventEntity.EventTypeCode != SchedulingConstants.ShiftEventTypeCode)
            throw new InvalidOperationException($"Event {eventEntity.Id} is not a shift event.");

        if (eventEntity.SourceModule != SchedulingConstants.SourceModule)
            throw new InvalidOperationException($"Event {eventEntity.Id} is not owned by Scheduling.");
    }

    public static void EnsureShiftEntryIsDraft(Event eventEntity)
    {
        if (eventEntity.StatusTypeCode != CalendarEventStatusTypeCodes.Draft)
            throw new InvalidOperationException("Shift entry must be in draft status to allow edits.");
    }
}
