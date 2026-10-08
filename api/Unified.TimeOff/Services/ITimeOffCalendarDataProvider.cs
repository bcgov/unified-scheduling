using Unified.Common.Calendar;
using Unified.TimeOff.Models.Calendar;

namespace Unified.TimeOff.Services;

public interface ITimeOffCalendarDataProvider
{
    Task<IReadOnlyCollection<TimeOffCalendarEvent>> GetEventsAsync(
        CalendarEventQueryContext queryContext,
        CancellationToken cancellationToken = default
    );
}
