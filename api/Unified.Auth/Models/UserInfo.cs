namespace Unified.Auth.Models;

public sealed record UserInfo(
    bool IsAuthenticated,
    string? Name,
    string? AuthenticationType,
    IReadOnlyList<UserClaim> Claims);

public sealed record UserClaim(string Type, string Value);
