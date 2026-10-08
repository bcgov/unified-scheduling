using Unified.TimeOff.Models;

namespace Unified.TimeOff.Services;

public interface ILeaveTypeService
{
    Task<IReadOnlyCollection<LeaveTypeResponse>> GetLeaveTypesAsync(CancellationToken cancellationToken = default);

    Task<LeaveTypeResponse?> GetLeaveTypeByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<LeaveTypeResponse> CreateLeaveTypeAsync(
        LeaveTypeRequest request,
        CancellationToken cancellationToken = default
    );

    Task<LeaveTypeResponse?> UpdateLeaveTypeAsync(
        int id,
        LeaveTypeRequest request,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteLeaveTypeAsync(int id, CancellationToken cancellationToken = default);
}
