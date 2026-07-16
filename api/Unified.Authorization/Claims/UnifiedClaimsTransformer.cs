using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Unified.Authorization;
using Unified.Db.Models.UserManagement;

namespace Unified.Authorization.Claims;

/// <summary>
/// Runs once per authenticated request. Resolves the signed-in user via the
/// account-resolution service, then adds the corresponding application claims
/// to the request identity.
/// </summary>
public sealed class UnifiedClaimsTransformer(IUserAccountResolutionService userAccountResolutionService)
    : IClaimsTransformation
{
    public const string ApplicationAuthenticationType = "UnifiedApplication";
    public const string ResolvedSubjectClaimType = "unified/resolved-sub";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (!principal.Identity?.IsAuthenticated ?? true)
            return principal;

        var externalSubject = GetExternalSubject(principal);
        var applicationIdentity = principal.Identities.FirstOrDefault(identity =>
            identity.AuthenticationType == ApplicationAuthenticationType
        );
        var hasLegacyApplicationClaims = HasLegacyApplicationClaims(principal);

        if (
            applicationIdentity is not null
            && applicationIdentity.FindFirst(ResolvedSubjectClaimType)?.Value == externalSubject
            && !hasLegacyApplicationClaims
        )
        {
            return principal;
        }

        var user = await userAccountResolutionService.ResolveCurrentUserAsync(principal, recordLogin: false);

        if (user == null)
            return principal;

        AddApplicationClaims(principal, user, externalSubject);

        return principal;
    }

    private static void AddApplicationClaims(ClaimsPrincipal principal, User user, string? externalSubject)
    {
        var permissions = user
            .ActiveUserRoles.SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.PermissionId)
            .Distinct()
            .ToList();

        var roles = user.ActiveUserRoles.Select(ur => ur.Role.Name).Distinct().ToList();

        var identity = new ClaimsIdentity(ApplicationAuthenticationType);

        AddClaimIfPresent(identity, ResolvedSubjectClaimType, externalSubject);
        AddClaimIfPresent(identity, UnifiedClaimTypes.UserId, user.Id.ToString());
        AddClaimIfPresent(identity, UnifiedClaimTypes.FirstName, user.FirstName);
        AddClaimIfPresent(identity, UnifiedClaimTypes.LastName, user.LastName);

        if (user.HomeLocationId.HasValue)
        {
            AddClaimIfPresent(identity, UnifiedClaimTypes.HomeLocationId, user.HomeLocationId.Value.ToString());

            if (!string.IsNullOrWhiteSpace(user.HomeLocation?.Timezone))
            {
                AddClaimIfPresent(identity, UnifiedClaimTypes.HomeLocationTimezone, user.HomeLocation.Timezone);
            }
        }

        identity.AddClaims(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        identity.AddClaims(permissions.Select(permission => new Claim(UnifiedClaimTypes.Permission, permission)));
        principal.AddIdentity(identity);
    }

    private static bool HasLegacyApplicationClaims(ClaimsPrincipal principal)
    {
        return principal
            .Identities.Where(identity => identity.AuthenticationType != ApplicationAuthenticationType)
            .SelectMany(identity => identity.Claims)
            .Any(IsApplicationClaim);
    }

    private static bool IsApplicationClaim(Claim claim)
    {
        return claim.Type
            is UnifiedClaimTypes.Permission
                or UnifiedClaimTypes.UserId
                or UnifiedClaimTypes.FirstName
                or UnifiedClaimTypes.LastName
                or UnifiedClaimTypes.HomeLocationId
                or UnifiedClaimTypes.HomeLocationTimezone
                or ClaimTypes.Role;
    }

    private static string? GetExternalSubject(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
    }

    private static void AddClaimIfPresent(ClaimsIdentity identity, string claimType, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        identity.AddClaim(new Claim(claimType, value));
    }
}
