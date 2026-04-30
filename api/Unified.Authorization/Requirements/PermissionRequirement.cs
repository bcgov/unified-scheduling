using Microsoft.AspNetCore.Authorization;

namespace Unified.Authorization.Requirements;

/// <summary>
/// Authorization requirement that demands the user holds a specific permission claim.
/// Registered as a named policy via <see cref="AuthorizationModule"/>.
/// </summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
