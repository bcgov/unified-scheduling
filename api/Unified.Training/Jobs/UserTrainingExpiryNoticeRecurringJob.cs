using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Unified.Common.Jobs;
using Unified.Training.Options;
using Unified.Training.Services;

namespace Unified.Training.Jobs;

public sealed class UserTrainingExpiryNoticeRecurringJob(
    IUserTrainingExpiryNotificationService notificationService,
    IOptionsMonitor<TrainingExpiryNotificationOptions> optionsMonitor,
    ILogger<UserTrainingExpiryNoticeRecurringJob> logger
) : IRecurringJob
{
    public string JobName => "training:user-training-expiry-email";

    public string CronSchedule =>
        optionsMonitor.CurrentValue.Enabled ? optionsMonitor.CurrentValue.CronSchedule : "disabled";

    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    public async Task Execute(PerformContext? context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting recurring job {JobName}", JobName);

        try
        {
            var sentCount = await notificationService.SendDueExpiryNoticesAsync(cancellationToken);

            logger.LogInformation(
                "Completed recurring job {JobName}; sent {SentCount} expiry notice(s)",
                JobName,
                sentCount
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Recurring job {JobName} canceled", JobName);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Recurring job {JobName} failed", JobName);
            throw;
        }
    }
}
