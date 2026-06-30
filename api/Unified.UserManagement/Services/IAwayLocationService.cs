using Unified.UserManagement.Models;

namespace Unified.UserManagement.Services;

public interface IAwayLocationService
{
    Task<IReadOnlyCollection<AwayLocationResponseDto>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );

    Task<AwayLocationResponseDto> CreateAsync(
        Guid userId,
        AwayLocationRequestDto request,
        CancellationToken cancellationToken = default
    );

    Task<AwayLocationResponseDto> UpdateAsync(
        Guid userId,
        int awayLocationId,
        AwayLocationRequestDto request,
        CancellationToken cancellationToken = default
    );

    Task<AwayLocationResponseDto> ExpireAsync(
        Guid userId,
        ExpireAwayLocationRequestDto request,
        CancellationToken cancellationToken = default
    );
}
