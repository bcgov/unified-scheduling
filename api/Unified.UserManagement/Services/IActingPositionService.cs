using Unified.UserManagement.Models;

namespace Unified.UserManagement.Services;

public interface IActingPositionService
{
    Task<IReadOnlyCollection<ActingPositionResponseDto>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );

    Task<ActingPositionResponseDto> CreateAsync(
        Guid userId,
        ActingPositionRequestDto request,
        CancellationToken cancellationToken = default
    );

    Task<ActingPositionResponseDto> UpdateAsync(
        Guid userId,
        int actingPositionId,
        ActingPositionRequestDto request,
        CancellationToken cancellationToken = default
    );

    Task<ActingPositionResponseDto> ExpireAsync(
        Guid userId,
        ExpireActingPositionRequestDto request,
        CancellationToken cancellationToken = default
    );
}
