using Unified.Common.Calendar;
using Unified.Leave.Models.Calendar;

namespace Unified.Leave.Services;

public interface ILeaveCalendarDataProvider
{
    Task<IReadOnlyCollection<LeaveCalendarEvent>> GetEventsAsync(
        CalendarEventQueryContext queryContext,
        CancellationToken cancellationToken = default
    );
}
