using Unified.UserManagement.Models;

namespace Unified.UserManagement.Services;

public interface IUserService
{
    Task<IReadOnlyCollection<UserResponse>> GetAllAsync(
        UserQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    );

    Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    Task<UserResponse?> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default);
}
