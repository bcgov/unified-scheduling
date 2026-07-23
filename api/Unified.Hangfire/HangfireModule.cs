using Hangfire;
using Hangfire.Console;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unified.Hangfire.Options;

namespace Unified.Hangfire;

/// <summary>
/// Registers Hangfire background job processing backed by the same PostgreSQL
/// database used by <c>UnifiedDbContext</c> (in its own "hangfire" schema).
/// Modules contribute <see cref="IRecurringJob"/> implementations via their own
/// <c>AddXxxModule()</c> calls; those jobs are automatically scheduled by <c>HangfireJobRegistrationService</c>.
///
/// Call <c>builder.Services.AddHangfireModule(builder.Configuration)</c> from Program.cs
/// AFTER all feature-gated modules have registered their services.
/// </summary>
public static class HangfireModule
{
    public static IServiceCollection AddHangfireModule(this IServiceCollection services, IConfiguration configuration)
    {
        var hangfireOptions = configuration.GetSection(HangfireOptions.SectionName).Get<HangfireOptions>() ?? new();
        if (!hangfireOptions.Enabled)
        {
            return services;
        }

        var connectionString = configuration.GetValue<string>("DatabaseConnectionString");
        if (string.IsNullOrEmpty(connectionString))
        {
            // No database configured (e.g. some test/local scenarios) - skip wiring Hangfire.
            return services;
        }

        services
            .AddOptions<HangfireOptions>()
            .BindConfiguration(HangfireOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Registered once here (rather than in HangfireJobRegistrationService, which runs as a
        // hosted service and could execute more than once, e.g. in tests) to avoid adding
        // duplicate global retry filters.
        var retryCount =
            configuration.GetValue<int?>($"{HangfireOptions.SectionName}:{nameof(HangfireOptions.RetryCount)}")
            ?? AutomaticRetryAttribute.DefaultRetryAttempts;
        GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute { Attempts = retryCount });

        services.AddHangfire(config =>
            config
                .UsePostgreSqlStorage(
                    c => c.UseNpgsqlConnection(connectionString),
                    new PostgreSqlStorageOptions
                    {
                        SchemaName = "hangfire",
                        QueuePollInterval = TimeSpan.FromSeconds(15),
                        JobExpirationCheckInterval = TimeSpan.FromMinutes(30),
                        CountersAggregateInterval = TimeSpan.FromMinutes(5),
                        PrepareSchemaIfNecessary = hangfireOptions.PrepareSchemaIfNecessary,
                        EnableTransactionScopeEnlistment = true,
                    }
                )
                // Captures per-job WriteLine output (see IRecurringJob.Execute) so it's visible
                // on each job's detail page in the dashboard, not just the state history.
                .UseConsole()
        );

        // Mirrors every ILogger call made during a job's execution (including nested service
        // calls, e.g. inside JCDataUpdaterService) to that job's Console tab in the dashboard.
        GlobalJobFilters.Filters.Add(new HangfireConsoleLoggingFilter());
        services.AddSingleton<ILoggerProvider, HangfireConsoleLoggerProvider>();
        services.AddTransient<HangfireRegistrationFailureMarkerJob>();

        if (hangfireOptions.ServerEnabled)
        {
            services.AddHangfireServer(options =>
            {
                options.WorkerCount = Math.Max(1, Environment.ProcessorCount / 4);
                options.SchedulePollingInterval = TimeSpan.FromSeconds(30);
            });
        }

        services.AddHostedService<HangfireJobRegistrationService>();

        return services;
    }
}
