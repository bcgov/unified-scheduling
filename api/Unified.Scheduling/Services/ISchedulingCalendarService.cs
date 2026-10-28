using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public interface ISchedulingCalendarService
{
    Task<SchedulingCalendarDataResponse> GetDataAsync(
        SchedulingCalendarRequest request,
        bool includeShifts,
        bool includeAssignments,
        bool includeLeave,
        bool includeLoanOut = false,
        CancellationToken cancellationToken = default
    );
}
