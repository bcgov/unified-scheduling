using Microsoft.AspNetCore.Authorization;
using Unified.Authorization.Claims;

namespace Unified.Authorization.Requirements;

/// <summary>
/// Succeeds when the authenticated user has a <see cref="UnifiedClaimTypes.Permission"/>
/// claim matching <see cref="PermissionRequirement.Permission"/>.
///
/// Throws <see cref="UnauthorizedAccessException"/> when the authenticated user lacks
/// the required permission, which is caught by the global exception handler and
/// returned as a 403 ProblemDetails response.
///
/// Permission claims are added at login by <see cref="PermissionClaimsTransformer"/>,
/// which expands the user's Keycloak role claims into permission claims.
/// </summary>
public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var hasPermission = context.User.Claims
            .Any(c => c.Type == UnifiedClaimTypes.Permission
                   && c.Value == requirement.Permission);

        if (hasPermission)
            context.Succeed(requirement);
        else
            throw new UnauthorizedAccessException(
                $"Missing required permission: {requirement.Permission}");

        return Task.CompletedTask;
    }
}
