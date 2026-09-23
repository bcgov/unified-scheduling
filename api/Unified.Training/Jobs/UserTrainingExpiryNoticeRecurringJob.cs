using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Unified.Common.Jobs;
using Unified.Core.Email;
using Unified.Training.Options;
using Unified.Training.Services;

namespace Unified.Training.Jobs;

public sealed class UserTrainingExpiryNoticeRecurringJob(
    IUserTrainingExpiryNotificationService notificationService,
    IOptionsMonitor<TrainingExpiryNotificationOptions> optionsMonitor,
    IEnumerable<IEmailService> emailServices,
    ILogger<UserTrainingExpiryNoticeRecurringJob> logger
) : IRecurringJob
{
    private readonly bool _hasEmailService = emailServices.Any();

    public string JobName => "training:user-training-expiry-email";

    public string CronSchedule
    {
        get
        {
            if (!optionsMonitor.CurrentValue.Enabled)
            {
                return "disabled";
            }

            if (_hasEmailService)
            {
                return optionsMonitor.CurrentValue.CronSchedule;
            }

            logger.LogWarning(
                "Training expiry recurring job is enabled in configuration but no email provider is registered; disabling job schedule"
            );
            return "disabled";
        }
    }

    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    public async Task Execute(PerformContext? context, CancellationToken cancellationToken)
    {
        if (!_hasEmailService)
        {
            logger.LogError(
                "Training expiry recurring job execution blocked because no email provider is registered while job is enabled"
            );
            throw new InvalidOperationException(
                "Training expiry recurring job cannot execute because no email provider is registered."
            );
        }

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
