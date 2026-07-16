using System.Security.Claims;
using Unified.Db.Models.UserManagement;

namespace Unified.Authorization;

public interface IUserAccountResolutionService
{
    Task UpdateCurrentUserLastLoginAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);

    Task<User?> ResolveCurrentUserAsync(
        ClaimsPrincipal principal,
        bool recordLogin = false,
        CancellationToken cancellationToken = default
    );
}
