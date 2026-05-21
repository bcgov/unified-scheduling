using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Unified.Authorization;
using Unified.Common.Seeding;
using Unified.UserManagement.Models;
using Unified.UserManagement.Seeders;
using Unified.UserManagement.Services;
using Unified.UserManagement.Validators;

namespace Unified.UserManagement;

/// <summary>
/// User management module extension for dependency injection and configuration
/// </summary>
public static class UserManagementModule
{
    private static readonly PermissionSeedDefinition[] PermissionSeeds =
    [
        // Users
        new(nameof(Permissions.UsersCreate), "Create new users"),
        new(nameof(Permissions.UsersEdit), "Edit existing users"),
        new(nameof(Permissions.UsersView), "View users"),
        new(nameof(Permissions.UsersExpire), "Expire users"),
        new(nameof(Permissions.UsersViewOtherProfiles), "View other user profiles"),
        // Roles
        new(nameof(Permissions.RolesView), "View roles"),
        new(nameof(Permissions.RolesCreateAndAssign), "Create and Assign Roles"),
        new(nameof(Permissions.RolesEdit), "Edit roles"),
        new(nameof(Permissions.RolesExpire), "Expire roles"),
    ];

    /// <summary>
    /// Add user management module services to the dependency injection container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddUserManagementModule(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();

        services.AddScoped<UserSeeder>();
        services.AddScoped<RegionSeeder>();
        services.AddScoped<LocationSeeder>();
        services.AddScoped<PermissionSeeder>();
        services.AddSingleton(new PermissionSeedConfiguration(nameof(UserManagementModule), PermissionSeeds));

        services.AddScoped<UserRequestValidator>();
        services.AddScoped<RoleRequestValidator>();
        services.AddScoped<UpdateRoleRequestValidator>();

        // Register permission policies owned by this module
        services
            .AddAuthorizationBuilder()
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
            .AddPermissionPolicy(Permissions.RolesExpire);

        return services;
    }
}
