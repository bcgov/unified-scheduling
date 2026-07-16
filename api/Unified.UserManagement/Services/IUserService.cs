using Unified.UserManagement.Models;

namespace Unified.UserManagement.Services;

public interface IUserService
{
    Task<IReadOnlyCollection<UserResponse>> GetAllAsync(
        UserQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    );

    Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UserResponse> CreateAsync(UserRequestDto request, CancellationToken cancellationToken = default);

    Task<UserResponse?> UpdateAsync(Guid id, UserRequestDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserRoleResponseDto>> GetRolesAsync(
        Guid id,
        CancellationToken cancellationToken = default
    );

    Task<UserRoleResponseDto> AssignRoleAsync(
        Guid id,
        AssignUserRoleRequestDto request,
        CancellationToken cancellationToken = default
    );

    Task<UserRoleResponseDto> ExpireRoleAsync(
        Guid id,
        ExpireUserRoleRequestDto request,
        CancellationToken cancellationToken = default
    );

    Task<byte[]?> GetPhotoAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UserResponse?> UploadPhotoAsync(Guid id, byte[] photo, CancellationToken cancellationToken = default);
}
