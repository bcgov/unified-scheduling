using Microsoft.Extensions.DependencyInjection;
using Unified.UserManagement.Seeders;
using Unified.UserManagement.Services;

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
        services.AddScoped<UserSeeder>();

        return services;
    }
}
