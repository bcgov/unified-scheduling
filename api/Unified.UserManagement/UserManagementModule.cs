using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Unified.Authorization;
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
        services.AddSingleton(UserManagementPermissionSeedData.Configuration);

        services.AddScoped<UserRequestValidator>();
        services.AddScoped<RoleRequestValidator>();
        services.AddScoped<UpdateRoleRequestValidator>();

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
            .AddPermissionPolicy(Permissions.RolesExpire);

        return services;
    }
}
