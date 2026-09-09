using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public interface ISchedulingCalendarService
{
    Task<SchedulingCalendarDataResponse> GetDataAsync(
        SchedulingCalendarRequest request,
        bool includeShifts,
        bool includeAssignments,
        CancellationToken cancellationToken = default
    );
}
