using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Unified.Authorization.Claims;
using Unified.Authorization.Requirements;

namespace Unified.Authorization;

/// <summary>
/// Registers all authorization services: the claims transformer, the handler,
/// and one named policy per permission constant.
///
/// Call <see cref="AddAuthorizationModule"/> from Program.cs after AddAuthentication.
/// </summary>
public static class AuthorizationModule
{
    /// <summary>
    /// Policy name prefix. Policies are named "Permission:{permissionName}".
    /// Use <see cref="BuildPolicyName"/> to build a policy name from a permission constant.
    /// </summary>
    public const string PolicyPrefix = "Permission:";

    /// <summary>
    /// Builds the policy name for a given permission constant.
    /// Example: BuildPolicyName(Permissions.ShiftsEdit) → "Permission:ShiftsEdit"
    /// </summary>
    public static string BuildPolicyName(string permission) => $"{PolicyPrefix}{permission}";

    public static IServiceCollection AddAuthorizationModule(this IServiceCollection services)
    {
        // Expand role claims → permission claims at authentication time
        services.AddScoped<IClaimsTransformation, UnifiedClaimsTransformer>();

        // Handler that checks permission claims
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        // Register cross-cutting permissions. Each feature module registers its
        // own permissions by calling AddAuthorizationBuilder().AddPermissionPolicy(...)
        // inside its own AddXxxModule() extension.

        return services;
    }

    /// <summary>
    /// Registers a single named permission policy on the authorization builder.
    /// Each feature module should call this for its own permissions rather than
    /// listing them all here — keeping policy ownership co-located with the module.
    ///
    /// Example usage inside a module's AddXxxModule:
    ///   services.AddAuthorizationBuilder()
    ///       .AddPermissionPolicy(Permissions.ShiftsView)
    ///       .AddPermissionPolicy(Permissions.ShiftsEdit);
    /// </summary>
    public static AuthorizationBuilder AddPermissionPolicy(this AuthorizationBuilder builder, string permission)
    {
        return builder.AddPolicy(
            BuildPolicyName(permission),
            policy => policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(permission))
        );
    }

    /// <summary>
    /// Registers a single named permission policy on the authorization builder from a permission enum value.
    /// </summary>
    public static AuthorizationBuilder AddPermissionPolicy(this AuthorizationBuilder builder, Permissions permission)
    {
        return builder.AddPermissionPolicy(permission.ToString());
    }
}
