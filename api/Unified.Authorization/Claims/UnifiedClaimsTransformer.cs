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
public sealed class UnifiedClaimsTransformer(UnifiedDbContext db) : IClaimsTransformation
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

        var user = await db
            .Users.Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ThenInclude(r => r.RolePermissions)
            .Where(u => u.IdirId == idir && u.IsEnabled)
            .FirstOrDefaultAsync();

        if (user == null)
            return principal;

        var permissions = user
            .ActiveUserRoles.SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.PermissionId)
            .Distinct()
            .ToList();

        var roles = user
            .ActiveUserRoles.Select(ur => ur.Role.Name)
            .Distinct()
            .ToList();

        var identity = (ClaimsIdentity)principal.Identity!;
        // Add Claims for the user's Idir ID, User ID, roles, and permissions. These claims are not stored in the cookie and only exist for the lifetime of the request.
        identity.AddClaim(new Claim(UnifiedClaimTypes.IdirId, idir.ToString()));
        identity.AddClaim(new Claim(UnifiedClaimTypes.UserId, user.Id.ToString()));
        identity.AddClaim(new Claim(UnifiedClaimTypes.FirstName, user.FirstName));
        identity.AddClaim(new Claim(UnifiedClaimTypes.LastName, user.LastName));
        if (user.HomeLocationId.HasValue)
            identity.AddClaim(new Claim(UnifiedClaimTypes.HomeLocationId, user.HomeLocationId.Value.ToString()));
        identity.AddClaims(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        identity.AddClaims(permissions.Select(p => new Claim(UnifiedClaimTypes.Permission, p)));

        return principal;
    }
}
