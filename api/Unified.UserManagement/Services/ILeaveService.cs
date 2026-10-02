using Unified.UserManagement.Models;

namespace Unified.UserManagement.Services;

public interface ILeaveService
{
    Task<IReadOnlyCollection<LeaveResponseDto>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );

    Task<LeaveResponseDto> CreateAsync(
        Guid userId,
        LeaveRequestDto request,
        CancellationToken cancellationToken = default
    );

    Task<LeaveResponseDto> UpdateAsync(
        Guid userId,
        int leaveId,
        LeaveRequestDto request,
        CancellationToken cancellationToken = default
    );

    Task<LeaveResponseDto> ExpireAsync(
        Guid userId,
        ExpireLeaveRequestDto request,
        CancellationToken cancellationToken = default
    );
}
