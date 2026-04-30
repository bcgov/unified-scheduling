using Unified.UserManagement.Models;

namespace Unified.UserManagement.Services;

public interface IRoleService
{
    Task<IReadOnlyCollection<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<RoleDto> CreateAsync(RoleRequestDto request, CancellationToken cancellationToken = default);
}
