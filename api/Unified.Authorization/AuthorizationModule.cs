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
    /// Use <see cref="PolicyName"/> to build a policy name from a permission constant.
    /// </summary>
    public const string PolicyPrefix = "Permission:";

    /// <summary>
    /// Builds the policy name for a given permission constant.
    /// Example: PolicyName(Permissions.EditShifts) → "Permission:EditShifts"
    /// </summary>
    public static string PolicyName(string permission) => $"{PolicyPrefix}{permission}";

    public static IServiceCollection AddAuthorizationModule(this IServiceCollection services)
    {
        // Expand role claims → permission claims at authentication time
        services.AddSingleton<IClaimsTransformation, PermissionClaimsTransformer>();

        // Handler that checks permission claims
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        // Register one policy per permission constant
        services.AddAuthorizationBuilder()
            .AddPermissionPolicy(Permissions.Login)
            .AddPermissionPolicy(Permissions.CreateUsers)
            .AddPermissionPolicy(Permissions.EditUsers)
            .AddPermissionPolicy(Permissions.ViewUsers)
            .AddPermissionPolicy(Permissions.ExpireUsers)
            .AddPermissionPolicy(Permissions.ViewRoles)
            .AddPermissionPolicy(Permissions.CreateAndAssignRoles)
            .AddPermissionPolicy(Permissions.EditRoles)
            .AddPermissionPolicy(Permissions.ExpireRoles)
            .AddPermissionPolicy(Permissions.ViewShifts)
            .AddPermissionPolicy(Permissions.CreateAndAssignShifts)
            .AddPermissionPolicy(Permissions.EditShifts)
            .AddPermissionPolicy(Permissions.ExpireShifts)
            .AddPermissionPolicy(Permissions.ImportShifts)
            .AddPermissionPolicy(Permissions.ViewDutyRoster)
            .AddPermissionPolicy(Permissions.CreateAndAssignDuties)
            .AddPermissionPolicy(Permissions.EditDuties)
            .AddPermissionPolicy(Permissions.ExpireDuties)
            .AddPermissionPolicy(Permissions.ViewHomeLocation)
            .AddPermissionPolicy(Permissions.ViewAssignedLocation)
            .AddPermissionPolicy(Permissions.ViewRegion)
            .AddPermissionPolicy(Permissions.ViewProvince)
            .AddPermissionPolicy(Permissions.GenerateReports);

        return services;
    }

    private static AuthorizationBuilder AddPermissionPolicy(
        this AuthorizationBuilder builder, string permission)
    {
        return builder.AddPolicy(
            PolicyName(permission),
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission)));
    }
}
