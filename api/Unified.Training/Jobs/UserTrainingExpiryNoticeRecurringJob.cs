using Hangfire.Server;
using Microsoft.Extensions.Options;
using Unified.Common.Jobs;
using Unified.Training.Options;
using Unified.Training.Services;

namespace Unified.Training.Jobs;

public sealed class UserTrainingExpiryNoticeRecurringJob(
    IUserTrainingExpiryNotificationService notificationService,
    IOptionsMonitor<TrainingExpiryNotificationOptions> optionsMonitor
) : IRecurringJob
{
    public string JobName => "training:user-training-expiry-email";

    public string CronSchedule =>
        optionsMonitor.CurrentValue.Enabled ? optionsMonitor.CurrentValue.CronSchedule : "disabled";

    public async Task Execute(PerformContext? context, CancellationToken cancellationToken)
    {
        await notificationService.SendDueExpiryNoticesAsync(cancellationToken);
    }
}
