using Microsoft.AspNetCore.Authorization;
using Unified.Authorization.Claims;
using Unified.Infrastructure.ErrorHandling;

namespace Unified.Authorization.Requirements;

/// <summary>
/// Succeeds when the authenticated user has a <see cref="UnifiedClaimTypes.Permission"/>
/// claim matching <see cref="PermissionRequirement.Permission"/>.
///
/// Throws <see cref="ForbiddenException"/> when an authenticated user lacks the required
/// permission; the global exception handler maps it to a 403 ProblemDetails response.
/// Unauthenticated requests are not failed here so that the standard authentication
/// challenge (401) flow is preserved.
///
/// Permission claims are added at login by <see cref="UnifiedClaimsTransformer"/>,
/// which expands the user's role claims into permission claims.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement
    )
    {
        // Let the authentication pipeline handle unauthenticated users (401 challenge).
        if (context.User.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        var hasPermission = context.User.Claims.Any(c =>
            c.Type == UnifiedClaimTypes.Permission && c.Value == requirement.Permission
        );

        if (hasPermission)
            context.Succeed(requirement);
        else
            throw new ForbiddenException($"Missing required permission: {requirement.Permission}");

        return Task.CompletedTask;
    }
}
