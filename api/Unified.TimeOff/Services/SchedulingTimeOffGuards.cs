using Unified.Db.Models.Calendar;
using Unified.TimeOff.Constants;

namespace Unified.TimeOff.Services;

internal static class SchedulingTimeOffGuards
{
    public static void EnsureTimeOffEventType(Event eventEntity)
    {
        if (eventEntity.EventTypeCode != TimeOffConstants.TimeOffEventTypeCode)
            throw new InvalidOperationException($"Event {eventEntity.Id} is not a time off event.");

        if (eventEntity.SourceModule != TimeOffConstants.SourceModule)
            throw new InvalidOperationException($"Event {eventEntity.Id} is not owned by TimeOff.");
    }

    public static void EnsureTimeOffEntryIsDraft(Event eventEntity)
    {
        if (eventEntity.StatusTypeCode != CalendarEventStatusTypeCodes.Draft)
            throw new InvalidOperationException("Time off entry must be in draft status to allow edits.");
    }

    public static void EnsureTimeOffEventSeriesType(EventSeries eventSeries)
    {
        if (eventSeries.EventTypeCode != TimeOffConstants.TimeOffEventTypeCode)
            throw new InvalidOperationException($"Event series {eventSeries.Id} is not a time off event series.");
    }

    public static void EnsureTimeOffSeriesIsDraft(EventSeries eventSeries)
    {
        if (eventSeries.StatusTypeCode != CalendarEventStatusTypeCodes.Draft)
            throw new InvalidOperationException("Time off series must be in draft status to allow edits.");
    }
}
