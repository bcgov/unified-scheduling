using FluentValidation;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Common.FeatureFlags;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement.FeatureFlags;
using Unified.UserManagement.Models;
using Unified.UserManagement.Options;
using Unified.UserManagement.Seeders;
using Unified.UserManagement.Services;
using Unified.Common.Options;
using Unified.UserManagement.Validators;

namespace Unified.UserManagement;

/// <summary>
/// User management module extension for dependency injection and configuration
/// </summary>
public static class UserManagementModule
{
    public static bool IsModuleEnabled(IConfiguration config)
    {
        var enabled = config
            .GetSection(UserManagementFeatureFlags.Section)
            .Get<UserManagementFeatureFlags>()?
            .Enabled ?? false;
        return enabled;
    }

    public static bool IsModuleEnabled(IServiceProvider serviceProvider)
    {
        var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<UserManagementFeatureFlags>>();
        return optionsMonitor.CurrentValue.Enabled;
    }

    /// <summary>
    /// Add user management module services to the dependency injection container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="config">Configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddUserManagementModule(this IServiceCollection services, IConfiguration config)
    {
        // Register UserManagement feature flags
        services
            .AddOptions<UserManagementFeatureFlags>()
            .BindConfiguration(UserManagementFeatureFlags.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<UserManagementFeatureFlags>, RequiredBooleanOptionsValidator<UserManagementFeatureFlags>>();

        // Register as IFeatureFlags for aggregation
        services.AddSingleton<IFeatureFlags>(sp =>
            sp.GetRequiredService<IOptionsMonitor<UserManagementFeatureFlags>>().CurrentValue
        );

        if (!IsModuleEnabled(config))
        {
            return services;
        }

        services.AddScoped<IUserAccountResolutionService, UserAccountResolutionService>();
        // Map PhotoUrl from Photo presence — expression is EF-translatable so both
        // ProjectToType (list) and Adapt (single user) populate it automatically.
        TypeAdapterConfig<User, UserResponse>
            .NewConfig()
            .Map(
                dest => dest.PhotoUrl,
                src => src.Photo != null && src.Photo.Length > 0 ? "/api/users/" + src.Id + "/photo" : null
            );

        services
            .AddOptions<UserManagementOptions>()
            .BindConfiguration(UserManagementOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IActingPositionService, ActingPositionService>();
        services.AddScoped<IAwayLocationService, AwayLocationService>();

        services.AddSeeder<UnifiedDbContext, UserSeeder>();
        services.AddSeeder<UnifiedDbContext, RoleSeeder>();
        services.AddSeeder<UnifiedDbContext, PermissionSeeder>();
        services.AddSeeder<UnifiedDbContext, RegionSeeder>();
        services.AddSeeder<UnifiedDbContext, LocationSeeder>();

        services.AddScoped<UserRequestValidator>();
        services.AddScoped<AssignUserRoleRequestValidator>();
        services.AddScoped<ExpireUserRoleRequestValidator>();
        services.AddScoped<RoleRequestValidator>();
        services.AddScoped<UpdateRoleRequestValidator>();
        services.AddScoped<DeleteRoleWithReassignmentRequestDtoValidator>();
        services.AddScoped<ActingPositionRequestValidator>();
        services.AddScoped<ExpireActingPositionRequestValidator>();
        services.AddScoped<AwayLocationRequestValidator>();
        services.AddScoped<ExpireAwayLocationRequestValidator>();

        // Register permission policies owned by this module
        services
            .AddAuthorizationBuilder()
            // Users
            .AddPermissionPolicy(Permissions.UsersCreate)
            .AddPermissionPolicy(Permissions.UsersEdit)
            .AddPermissionPolicy(Permissions.UserRoleAssign)
            .AddPermissionPolicy(Permissions.UsersView)
            .AddPermissionPolicy(Permissions.UsersExpire)
            .AddPermissionPolicy(Permissions.UsersViewOtherProfiles)
            // Roles
            .AddPermissionPolicy(Permissions.RolesView)
            .AddPermissionPolicy(Permissions.RolesCreate)
            .AddPermissionPolicy(Permissions.RolesEdit)
            .AddPermissionPolicy(Permissions.RolesExpire)
            // Acting Positions
            .AddPermissionPolicy(Permissions.ActingPositionsView)
            .AddPermissionPolicy(Permissions.ActingPositionsCreate)
            .AddPermissionPolicy(Permissions.ActingPositionsEdit)
            .AddPermissionPolicy(Permissions.ActingPositionsExpire)
            // Admin
            .AddPermissionPolicy(Permissions.HangfireDashboardView)
            // Away Locations
            .AddPermissionPolicy(Permissions.AwayLocationsView)
            .AddPermissionPolicy(Permissions.AwayLocationsCreate)
            .AddPermissionPolicy(Permissions.AwayLocationsEdit)
            .AddPermissionPolicy(Permissions.AwayLocationsExpire);

        return services;
    }
}
