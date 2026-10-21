using Unified.Common.Calendar.Scheduling.Models;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public interface ICalendarEventSeriesService<TRequest, TEntity> : ICalendarEventService<TRequest, TEntity, EventSeries>
    where TRequest : CalendarEventSeriesRequest
{
    /// <summary>
    /// Detects recurrence changes before an update mutates the existing series.
    /// </summary>
    bool HasRecurrenceChanged(TEntity entity, TRequest request);
}
