using Unified.Authorization;

namespace Unified.UserManagement.Models;

public sealed record UserInfo(
    bool IsAuthenticated,
    string? Name,
    string? AuthenticationType,
    IReadOnlyList<UserClaim> Claims,
    IReadOnlyList<Permissions> Permissions,
    Guid? UserId
);

public sealed record UserClaim(string Type, string Value);
