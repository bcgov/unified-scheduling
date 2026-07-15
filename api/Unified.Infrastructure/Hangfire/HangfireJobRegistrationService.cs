using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Unified.Common.Jobs;
using Unified.Infrastructure.Options;

namespace Unified.Infrastructure.Hangfire;

/// <summary>
/// Background service that registers Hangfire recurring jobs after the application has
/// fully started. This prevents blocking application startup on job dependencies (e.g.
/// database connections) and lets all modules' <see cref="IRecurringJob"/> contributions
/// be discovered via DI.
/// </summary>
public sealed class HangfireJobRegistrationService(
    IServiceProvider serviceProvider,
    ILogger<HangfireJobRegistrationService> logger,
    IOptions<HangfireOptions> hangfireOptions
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Wait a bit to ensure the application has fully started.
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            logger.LogInformation("Registering Hangfire recurring jobs...");

            using var scope = serviceProvider.CreateScope();
            var allJobs = scope.ServiceProvider.GetServices<IRecurringJob>();
            var retryCount = hangfireOptions.Value.RetryCount;

            GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute { Attempts = retryCount });

            foreach (var job in allJobs)
            {
                RecurringJobHelper.AddOrUpdate(job);

                logger.LogInformation("Registered recurring job: {JobType}", job.GetType().Name);
            }

            logger.LogInformation(
                "All Hangfire recurring jobs registered successfully with retry count {RetryCount}",
                retryCount
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register Hangfire recurring jobs");
        }
    }
}
