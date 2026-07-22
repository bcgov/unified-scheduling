using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Unified.Common.Jobs;

namespace Unified.Infrastructure.Hangfire;

/// <summary>
/// Background service that registers Hangfire recurring jobs after the application has
/// fully started. This prevents blocking application startup on job dependencies (e.g.
/// database connections) and lets all modules' <see cref="IRecurringJob"/> contributions
/// be discovered via DI.
/// </summary>
public sealed class HangfireJobRegistrationService(
    IServiceProvider serviceProvider,
    ILogger<HangfireJobRegistrationService> logger
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
            var failedJobs = new List<string>();
            var registeredCount = 0;

            foreach (var job in allJobs)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    logger.LogInformation("Hangfire recurring job registration was canceled during shutdown");
                    break;
                }

                var jobType = job.GetType().Name;
                try
                {
                    RecurringJobHelper.AddOrUpdate(job);
                    registeredCount++;
                    logger.LogInformation("Registered recurring job: {JobType}", jobType);
                }
                catch (Exception ex)
                {
                    failedJobs.Add(jobType);
                    logger.LogError(ex, "Failed to register recurring job: {JobType}", jobType);

                    // Create a failed Hangfire job entry so registration failures are visible
                    // directly in the dashboard's Failed Jobs tab.
                    var failureSummary = $"{ex.GetType().Name}: {ex.Message}";
                    TryCreateDashboardFailureMarker(jobType, failureSummary);
                }
            }

            if (failedJobs.Count == 0)
            {
                logger.LogInformation("All Hangfire recurring jobs registered successfully ({RegisteredCount} jobs)", registeredCount);
            }
            else
            {
                logger.LogWarning(
                    "Completed Hangfire recurring job registration with {FailedCount} failures out of {TotalCount} jobs. Failed jobs: {FailedJobTypes}",
                    failedJobs.Count,
                    registeredCount + failedJobs.Count,
                    string.Join(", ", failedJobs)
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register Hangfire recurring jobs");
        }
    }

    private void TryCreateDashboardFailureMarker(string jobType, string failureSummary)
    {
        try
        {
            BackgroundJob.Enqueue<HangfireRegistrationFailureMarkerJob>(job =>
                job.Execute(jobType, failureSummary)
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not enqueue Hangfire registration failure marker for {JobType}",
                jobType
            );
        }
    }
}
