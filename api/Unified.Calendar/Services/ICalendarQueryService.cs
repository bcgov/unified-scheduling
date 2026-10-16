using Unified.Calendar.Models;

namespace Unified.Calendar.Services;

public interface ICalendarQueryService
{
    Task<CalendarDataResponse> GetCalendarDataAsync(
        CalendarDataRequest request,
        CancellationToken cancellationToken = default
    );
}
