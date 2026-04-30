using Microsoft.AspNetCore.Builder;

namespace Unified.Authorization;

/// <summary>
/// Fluent extension for securing Minimal API endpoints with permission policies.
/// 
/// NOTE: The project currently uses MVC controllers. Prefer the [Authorize] attribute pattern:
///   [Authorize(Policy = AuthorizationModule.PolicyName(Permissions.EditShifts))]
///
/// This extension is kept for cases where Minimal API route groups are used.
/// </summary>
public static class EndpointAuthorizationExtensions
{
    /// <summary>
    /// Requires the caller to hold the specified permission.
    /// </summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(AuthorizationModule.PolicyName(permission));
}
