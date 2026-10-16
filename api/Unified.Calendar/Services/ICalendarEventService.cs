using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Calendar.Services;

public interface ICalendarEventService<TRequest, TEntity>
    where TRequest : CalendarEventBase
{
    TEntity BuildEvent(TRequest request, string sourceModule, string eventTypeCode, int? eventSeriesId = null);

    TEntity? UpdateEvent(TRequest request, TEntity existing, string sourceModule, string eventTypeCode);

    bool IsDeletable(TEntity entity);
}
