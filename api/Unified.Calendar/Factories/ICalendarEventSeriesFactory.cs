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
}
