using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Unified.Auth.Services;

namespace Unified.Auth;

/// <summary>
/// Auth module extension for dependency injection and configuration
/// </summary>
public static class AuthModule
{
    /// <summary>
    /// Add auth module services to the dependency injection container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
