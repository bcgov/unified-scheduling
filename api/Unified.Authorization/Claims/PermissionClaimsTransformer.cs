using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Unified.Db;

namespace Unified.Authorization.Claims;

/// <summary>
/// Runs once per authenticated request. Reads the user's Keycloak ID from the
/// <c>sub</c> claim, loads their active role permissions from the database, and
/// adds the corresponding permission claims to the identity.
/// </summary>
public sealed class PermissionClaimsTransformer(UnifiedDbContext db) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (!principal.Identity?.IsAuthenticated ?? true)
            return principal;

        // Avoid adding duplicates if transform is called multiple times
        if (principal.HasClaim(c => c.Type == UnifiedClaimTypes.Permission))
            return principal;

        var nameIdentifier = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(nameIdentifier))
            return principal;

        var idir = Guid.Parse(nameIdentifier.Replace("@idir", ""));

        var permissions = await db.Users
            .Where(u => u.IdirId == idir && u.IsEnabled)
            .SelectMany(u => u.UserRoles)
            .Where(ur =>
                ur.EffectiveDate <= DateTimeOffset.UtcNow
                && (ur.ExpiryDate == null || ur.ExpiryDate > DateTimeOffset.UtcNow)
            )
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.PermissionId)
            .Distinct()
            .ToListAsync();

        var identity = (ClaimsIdentity)principal.Identity!;
        // Add Claims for the user's Idir ID and permissions. These claims are not stored in the cookie and only exist for the lifetime of the request.
        identity.AddClaim(new Claim(UnifiedClaimTypes.IdirId, idir.ToString()));
        identity.AddClaims(permissions.Select(p => new Claim(UnifiedClaimTypes.Permission, p)));

        return principal;
    }
}
