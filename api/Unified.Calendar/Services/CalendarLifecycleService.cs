using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public sealed class CalendarLifecycleService : ICalendarLifecycleService
{
    public void Publish(Event eventEntity)
    {
        EnsureDraft(eventEntity.StatusTypeCode, "Calendar event");
        eventEntity.StatusTypeCode = CalendarEventStatusTypeCodes.Active;
    }

    public void PublishSeries(EventSeries eventSeries, IReadOnlyCollection<Event> childEvents)
    {
        Publish(eventSeries);
        foreach (var childEvent in childEvents)
        {
            if (childEvent.StatusTypeCode == CalendarEventStatusTypeCodes.Draft)
                Publish(childEvent);
        }
    }

    public void Cancel(
        Event eventEntity,
        DateTimeOffset cancelledAt,
        Guid? cancelledByUserId,
        string? cancellationReason
    )
    {
        eventEntity.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;
        eventEntity.CancelledAt = cancelledAt;
        eventEntity.CancelledByUserId = cancelledByUserId;
        eventEntity.CancellationReason = cancellationReason?.Trim();
    }

    public void CancelSeries(
        EventSeries eventSeries,
        IReadOnlyCollection<Event> childEvents,
        DateTimeOffset cancelledAt,
        Guid? cancelledByUserId,
        string? cancellationReason
    )
    {
        Cancel(eventSeries, cancelledAt, cancelledByUserId, cancellationReason);
        foreach (var childEvent in childEvents)
        {
            if (childEvent.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
                Cancel(childEvent, cancelledAt, cancelledByUserId, cancellationReason);
        }
    }

    public bool CanDelete(Event eventEntity)
    {
        return eventEntity.StatusTypeCode == CalendarEventStatusTypeCodes.Draft;
    }

    public bool CanDeleteSeries(EventSeries eventSeries, IReadOnlyCollection<Event> childEvents)
    {
        return eventSeries.StatusTypeCode == CalendarEventStatusTypeCodes.Draft;
    }

    public void ValidateNormalUpdateDoesNotChangeStatus(Event existingEvent, Event updatedEvent)
    {
        if (
            existingEvent.StatusTypeCode == updatedEvent.StatusTypeCode
            && existingEvent.CancelledAt == updatedEvent.CancelledAt
            && existingEvent.CancelledByUserId == updatedEvent.CancelledByUserId
            && existingEvent.CancellationReason == updatedEvent.CancellationReason
        )
            return;

        throw new InvalidOperationException("Calendar event lifecycle fields cannot be changed by a normal update.");
    }

    public void ValidateNormalSeriesUpdateDoesNotChangeStatus(
        EventSeries existingEventSeries,
        EventSeries updatedEventSeries
    )
    {
        if (
            existingEventSeries.StatusTypeCode == updatedEventSeries.StatusTypeCode
            && existingEventSeries.CancelledAt == updatedEventSeries.CancelledAt
            && existingEventSeries.CancelledByUserId == updatedEventSeries.CancelledByUserId
            && existingEventSeries.CancellationReason == updatedEventSeries.CancellationReason
        )
            return;

        throw new InvalidOperationException(
            "Calendar event series lifecycle fields cannot be changed by a normal update."
        );
    }

    private static void Publish(EventSeries eventSeries)
    {
        EnsureDraft(eventSeries.StatusTypeCode, "Calendar event series");
        eventSeries.StatusTypeCode = CalendarEventStatusTypeCodes.Active;
    }

    private static void Cancel(
        EventSeries eventSeries,
        DateTimeOffset cancelledAt,
        Guid? cancelledByUserId,
        string? cancellationReason
    )
    {
        eventSeries.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;
        eventSeries.CancelledAt = cancelledAt;
        eventSeries.CancelledByUserId = cancelledByUserId;
        eventSeries.CancellationReason = cancellationReason?.Trim();
    }

    private static void EnsureDraft(string statusTypeCode, string entityName)
    {
        if (statusTypeCode != CalendarEventStatusTypeCodes.Draft)
            throw new InvalidOperationException($"{entityName} must be in draft status to publish.");
    }
}
