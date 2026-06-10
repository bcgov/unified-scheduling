using Unified.Calendar.Models;

namespace Unified.Calendar.Services;

public interface ICalendarEventService
{
    Task<IReadOnlyCollection<CalendarEventResponse>> GetEventsAsync(
        CalendarEventsRequest request,
        CancellationToken cancellationToken = default
    );
}
