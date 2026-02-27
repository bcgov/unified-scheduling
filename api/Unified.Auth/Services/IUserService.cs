using Unified.Auth.Models;

namespace Unified.Auth.Services;

public interface IUserService
{
    Task<IReadOnlyCollection<User>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    Task<User?> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
