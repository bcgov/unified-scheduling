using Unified.UserManagement.Models;

namespace Unified.UserManagement.Services;

public interface IRoleService
{
    Task<IReadOnlyCollection<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RoleAssignedUserDto>> GetAssignedUsersAsync(
        int roleId,
        CancellationToken cancellationToken = default
    );

    Task<RoleDto> CreateAsync(RoleRequestDto request, CancellationToken cancellationToken = default);

    Task<RoleDto> UpdateAsync(UpdateRoleRequestDto request, CancellationToken cancellationToken = default);

    Task<DeletedRoleDto> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<DeletedRoleDto> ReassingAndDeleteAsync(
        int roleIdToDelete,
        DeleteRoleWithReassignmentRequestDto request,
        CancellationToken cancellationToken = default
    );
}
