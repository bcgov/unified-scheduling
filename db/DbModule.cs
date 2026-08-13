using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Common.Interceptors;
using Unified.Db.Models;

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

        services.AddScoped<SaveRulesInterceptor>();

        services.AddDbContext<UnifiedDbContext>(
            (serviceProvider, options) =>
            {
                options.UseNpgsql(connectionString);
                options.ConfigureWarnings(w =>
                    w.Ignore(RelationalEventId.PendingModelChangesWarning)
                );

                // Register SaveRulesInterceptor to run all ISaveRules before SaveChanges
                var interceptor = serviceProvider.GetRequiredService<SaveRulesInterceptor>();
                options.AddInterceptors(interceptor);
            }
        );

        // Register context factory for read-only queries inside save rules.
        services.AddDbContextFactory<UnifiedDbContext>(
            options =>
            {
                options.UseNpgsql(connectionString);
                options.ConfigureWarnings(w =>
                    w.Ignore(RelationalEventId.PendingModelChangesWarning)
                );
            },
            ServiceLifetime.Scoped
        );

        services.AddSingleton(configuration);

        return services;
    }
}
