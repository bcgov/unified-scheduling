using Unified.Calendar.Models;
using Unified.Common.Calendar.Scheduling.Models;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Factories;

public interface ICalendarEventSeriesFactory<TRequest, TResult>
    where TRequest : CalendarEventSeriesRequest
{
    TResult CreateSeries(TRequest request, EventSeries eventSeries);

    EventSeries GetEventSeries(TResult entity);

    /// <summary>
    /// Updates domain fields after the shared parent mapping. Use context.RecurrenceChanged and
    /// context.PreviousValues rather than comparing the already-mutated parent to the request.
    /// The domain caller coordinates regeneration or child propagation, persistence and conflicts.
    /// </summary>
    TResult UpdateSeries(TRequest request, TResult existing);

    /// <summary>
    /// Detects recurrence-affecting changes between the persisted series and the incoming request.
    /// Call before the shared parent mapping mutates <paramref name="eventSeries"/>; a true result means
    /// existing occurrences must be regenerated rather than patched.
    /// </summary>
    bool HasRecurrenceChanged(EventSeries eventSeries, TRequest request) =>
        !string.Equals(eventSeries.RecurrenceRule?.Trim(), request.RecurrenceRule?.Trim(), StringComparison.Ordinal)
        || eventSeries.StartAtUtc != request.StartAtUtc
        || eventSeries.EndAtUtc != request.EndAtUtc
        || !string.Equals(eventSeries.TimeZoneId?.Trim(), request.TimeZoneId?.Trim(), StringComparison.Ordinal)
        || eventSeries.AllDay != request.AllDay;
}
