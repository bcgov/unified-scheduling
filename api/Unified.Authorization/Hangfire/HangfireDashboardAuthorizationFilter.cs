using Hangfire.Dashboard;
using Unified.Authorization.Claims;

namespace Unified.Authorization.Hangfire;

/// <summary>
/// Restricts access to the Hangfire dashboard ("/hangfire") to authenticated users who
/// hold the <see cref="Permissions.HangfireDashboardView"/> permission.
///
/// Hangfire's dashboard authorization runs outside the normal MVC authorization pipeline,
/// so it cannot use an <c>[Authorize(Policy = ...)]</c> policy directly - the permission
/// claim (added by <c>UnifiedClaimsTransformer</c>) is checked here instead.
/// </summary>
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var user = context.GetHttpContext().User;

        return user.Identity?.IsAuthenticated == true
            && user.HasClaim(UnifiedClaimTypes.Permission, nameof(Permissions.HangfireDashboardView));
    }
}
