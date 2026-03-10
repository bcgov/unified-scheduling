using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Auth.Services;
using Unified.Auth.Services.EF;
using Unified.Db.Models;

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
        services.AddDbContext<UnifiedDbContext>(
            (serviceProvider, options) =>
            {
                var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                var connectionString = configuration.GetValue<string>("DatabaseConnectionString");

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException(
                        "DatabaseConnectionString configuration value is required for auth database."
                    );
                }

                options.UseNpgsql(connectionString);
            }
        );

        services.AddSingleton<MigrationAndSeedService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }
}
