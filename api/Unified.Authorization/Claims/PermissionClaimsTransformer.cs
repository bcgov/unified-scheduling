using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Unified.Authorization.Claims;

/// <summary>
/// Runs once per authenticated request. Reads the user's Keycloak role claims
/// and adds the corresponding permission claims to the identity.
///
/// TODO: When the database-backed permission model is ready, inject
/// IPermissionService here and replace the hardcoded permissions map
/// with an async DB lookup keyed by role name.
/// </summary>
public sealed class PermissionClaimsTransformer : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (!principal.Identity?.IsAuthenticated ?? true)
            return Task.FromResult(principal);

        // Avoid adding duplicates if transform is called multiple times
        if (principal.HasClaim(c => c.Type == UnifiedClaimTypes.Permission))
            return Task.FromResult(principal);

        var identity = (ClaimsIdentity)principal.Identity!;

        // @TODO: Get Permissions from Database
        var permissions = Array.Empty<string>();

        foreach (var permission in permissions)
            identity.AddClaim(new Claim(UnifiedClaimTypes.Permission, permission));

        return Task.FromResult(principal);
    }
}
