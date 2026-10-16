using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

/// <summary>
/// Reusable invariant checks shared by every domain that builds on the generic calendar entry/series
/// pipeline (<see cref="CalendarEventEntryService{TRequest,TEntity}"/> /
/// <see cref="CalendarEventSeriesService{TRequest,TEntity}"/>). Domain modules (e.g. Leave) supply
/// their own expected event type code / source module values rather than hard-coding them here.
/// </summary>
public static class CalendarEventGuards
{
    public static void EnsureEventType(Event eventEntity, string expectedEventTypeCode, string expectedSourceModule)
    {
        if (eventEntity.EventTypeCode != expectedEventTypeCode)
            throw new InvalidOperationException($"Event {eventEntity.Id} is not a {expectedEventTypeCode} event.");

        if (eventEntity.SourceModule != expectedSourceModule)
            throw new InvalidOperationException($"Event {eventEntity.Id} is not owned by {expectedSourceModule}.");
    }

    public static void EnsureEntryIsDraft(Event eventEntity)
    {
        if (eventEntity.StatusTypeCode != CalendarEventStatusTypeCodes.Draft)
            throw new InvalidOperationException("Calendar entry must be in draft status to allow edits.");
    }

    public static void EnsureEventSeriesType(EventSeries eventSeries, string expectedEventTypeCode)
    {
        if (eventSeries.EventTypeCode != expectedEventTypeCode)
            throw new InvalidOperationException(
                $"Event series {eventSeries.Id} is not a {expectedEventTypeCode} event series."
            );
    }

    public static void EnsureSeriesIsDraft(EventSeries eventSeries)
    {
        if (eventSeries.StatusTypeCode != CalendarEventStatusTypeCodes.Draft)
            throw new InvalidOperationException("Calendar series must be in draft status to allow edits.");
    }
}
