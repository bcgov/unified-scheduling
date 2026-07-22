using System.Reflection;
using Hangfire;
using Hangfire.Console;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unified.Common.Jobs;
using Unified.Infrastructure.Options;

namespace Unified.Infrastructure.Hangfire;

/// <summary>
/// Registers Hangfire background job processing backed by the same PostgreSQL
/// database used by <c>UnifiedDbContext</c> (in its own "hangfire" schema), auto-discovers
/// every <see cref="IRecurringJob"/> implementation across the "Unified.*" assemblies, and
/// registers the startup service that schedules them.
///
/// Call <c>builder.Services.AddHangfireModule(builder.Configuration)</c> from Program.cs
/// (or a shared module such as <c>AddInfrastructureModule</c>).
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
                        PrepareSchemaIfNecessary = true,
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

        if (hangfireOptions.ServerEnabled)
        {
            services.AddHangfireServer(options =>
            {
                options.WorkerCount = Math.Max(1, Environment.ProcessorCount / 4);
                options.SchedulePollingInterval = TimeSpan.FromSeconds(30);
            });
        }

        // Auto-register every IRecurringJob implementation found across the "Unified.*"
        // assemblies, so modules don't need their own AddTransient<IRecurringJob, TJob>()
        // call in Program.cs - implementing the interface is enough. The concrete type is
        // also registered directly (not just as IRecurringJob) because Hangfire's JobActivator
        // resolves and invokes jobs by their concrete type at execution time.
        foreach (var jobType in DiscoverRecurringJobTypes())
        {
            services.AddTransient(jobType);
            services.AddTransient(typeof(IRecurringJob), sp => sp.GetRequiredService(jobType));
        }

        services.AddHostedService<HangfireJobRegistrationService>();

        return services;
    }

    /// <summary>
    /// Walks the assembly reference graph starting at the entry assembly, following only
    /// "Unified.*" references (forcing them to load if not already), and returns every
    /// concrete class implementing <see cref="IRecurringJob"/>. Walking the reference graph
    /// (rather than relying on <c>AppDomain.CurrentDomain.GetAssemblies()</c>) ensures jobs are
    /// found regardless of how early this module is registered relative to other modules.
    /// </summary>
    private static IEnumerable<Type> DiscoverRecurringJobTypes()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly is null)
        {
            return [];
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var toVisit = new Queue<Assembly>([entryAssembly]);
        var jobTypes = new List<Type>();

        while (toVisit.Count > 0)
        {
            var assembly = toVisit.Dequeue();
            var name = assembly.GetName().Name;
            if (name is null || !visited.Add(name))
            {
                continue;
            }

            jobTypes.AddRange(
                GetLoadableTypes(assembly)
                    .Where(t => t.IsClass && !t.IsAbstract && typeof(IRecurringJob).IsAssignableFrom(t))
            );

            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (reference.Name?.StartsWith("Unified.", StringComparison.Ordinal) == true)
                {
                    toVisit.Enqueue(Assembly.Load(reference));
                }
            }
        }

        return jobTypes;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
