using Unified.Calendar.Models;

namespace Unified.Calendar.Services;

public interface ICalendarEventService
{
    Task<CalendarDataResponse> GetCalendarDataAsync(
        CalendarDataRequest request,
        CancellationToken cancellationToken = default
    );
}
