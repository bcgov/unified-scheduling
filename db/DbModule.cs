using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Unified.Db;

public static class DbModule
{
    public static IServiceCollection AddDbModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString = configuration["DatabaseConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "DatabaseConnectionString configuration value is required for database setup."
            );
        }

        services.AddDbContext<UnifiedDbContext>(
            (serviceProvider, options) =>
            {
                options.UseNpgsql(connectionString);
                options.ConfigureWarnings(w =>
                    w.Ignore(RelationalEventId.PendingModelChangesWarning)
                );

                var interceptors = serviceProvider.GetServices<IInterceptor>().ToArray();
                if (interceptors.Length > 0)
                {
                    options.AddInterceptors(interceptors);
                }
            }
        );

        services.AddSingleton(configuration);

        return services;
    }
}
