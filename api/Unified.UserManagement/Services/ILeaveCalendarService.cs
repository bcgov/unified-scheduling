using Unified.UserManagement.Models;

namespace Unified.UserManagement.Services;

public interface ILeaveCalendarService
{
    Task<LeaveCalendarDataResponse> GetLeaveCalendarDataAsync(
        LeaveCalendarRequest request,
        CancellationToken cancellationToken = default
    );
}
