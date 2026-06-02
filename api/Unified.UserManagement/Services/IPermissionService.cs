using Unified.UserManagement.Models;

namespace Unified.UserManagement.Services;

public interface IPermissionService
{
    Task<IReadOnlyCollection<PermissionDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
