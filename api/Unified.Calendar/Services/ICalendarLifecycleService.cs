using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public interface ICalendarLifecycleService
{
    void Publish(Event eventEntity);

    void PublishSeries(EventSeries eventSeries, IReadOnlyCollection<Event> childEvents);

    void Cancel(Event eventEntity, DateTimeOffset cancelledAt, Guid? cancelledByUserId, string? cancellationReason);

    void CancelSeries(
        EventSeries eventSeries,
        IReadOnlyCollection<Event> childEvents,
        DateTimeOffset cancelledAt,
        Guid? cancelledByUserId,
        string? cancellationReason
    );

    bool CanDelete(Event eventEntity);

    bool CanDeleteSeries(EventSeries eventSeries, IReadOnlyCollection<Event> childEvents);

    void ValidateNormalUpdateDoesNotChangeStatus(Event existingEvent, Event updatedEvent);

    void ValidateNormalSeriesUpdateDoesNotChangeStatus(EventSeries existingEventSeries, EventSeries updatedEventSeries);
}
