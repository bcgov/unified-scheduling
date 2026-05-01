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
    /// Example: PolicyName(Permissions.ShiftsEdit) → "Permission:ShiftsEdit"
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
            .AddPermissionPolicy(Permissions.AuthLogin)
            // Users
            .AddPermissionPolicy(Permissions.UsersCreate)
            .AddPermissionPolicy(Permissions.UsersEdit)
            .AddPermissionPolicy(Permissions.UsersView)
            .AddPermissionPolicy(Permissions.UsersExpire)
            .AddPermissionPolicy(Permissions.UsersViewOtherProfiles)
            // Roles
            .AddPermissionPolicy(Permissions.RolesView)
            .AddPermissionPolicy(Permissions.RolesCreateAndAssign)
            .AddPermissionPolicy(Permissions.RolesEdit)
            .AddPermissionPolicy(Permissions.RolesExpire)
            // Types
            .AddPermissionPolicy(Permissions.TypesCreate)
            .AddPermissionPolicy(Permissions.TypesEdit)
            .AddPermissionPolicy(Permissions.TypesExpire)
            // Shifts
            .AddPermissionPolicy(Permissions.ShiftsView)
            .AddPermissionPolicy(Permissions.ShiftsCreateAndAssign)
            .AddPermissionPolicy(Permissions.ShiftsEdit)
            .AddPermissionPolicy(Permissions.ShiftsExpire)
            .AddPermissionPolicy(Permissions.ShiftsImport)
            .AddPermissionPolicy(Permissions.ShiftsViewAllFuture)
            // Schedule
            .AddPermissionPolicy(Permissions.ScheduleViewDistribute)
            // Assignments
            .AddPermissionPolicy(Permissions.AssignmentsCreate)
            .AddPermissionPolicy(Permissions.AssignmentsEdit)
            .AddPermissionPolicy(Permissions.AssignmentsExpire)
            // Duty Roster
            .AddPermissionPolicy(Permissions.DutyRosterView)
            .AddPermissionPolicy(Permissions.DutyRosterViewFuture)
            // Duties
            .AddPermissionPolicy(Permissions.DutiesCreateAndAssign)
            .AddPermissionPolicy(Permissions.DutiesEdit)
            .AddPermissionPolicy(Permissions.DutiesExpire)
            // Location
            .AddPermissionPolicy(Permissions.LocationViewHome)
            .AddPermissionPolicy(Permissions.LocationViewAssigned)
            .AddPermissionPolicy(Permissions.LocationViewRegion)
            .AddPermissionPolicy(Permissions.LocationViewProvince)
            .AddPermissionPolicy(Permissions.LocationExpire)
            // Training
            .AddPermissionPolicy(Permissions.TrainingEditPast)
            .AddPermissionPolicy(Permissions.TrainingRemovePast)
            .AddPermissionPolicy(Permissions.TrainingAdjustExpiry)
            .AddPermissionPolicy(Permissions.TrainingExempt)
            // IDIR
            .AddPermissionPolicy(Permissions.IdirEdit)
            // Reports
            .AddPermissionPolicy(Permissions.ReportsGenerate);

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
