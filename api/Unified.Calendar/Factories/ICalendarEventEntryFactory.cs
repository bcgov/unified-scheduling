using Unified.Common.Calendar.Scheduling.Models;
using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Factories;

public interface ICalendarEventEntryFactory<TRequest, TEntity>
    where TRequest : CalendarEventEntryRequest
{
    TEntity CreateEntry(TRequest request, Event eventEntity);

    Event GetEvent(TEntity entity);

    TEntity UpdateEntry(TRequest request, TEntity existing);
}
