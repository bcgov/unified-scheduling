using Hangfire.Server;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Unified.Common.Jobs;
using Unified.JCInterface.Options;
using Unified.JCInterface.Services;

namespace Unified.JCInterface.Jobs;

/// <summary>
/// Runs the full JC Interface sync pipeline on its configured Hangfire schedule.
/// </summary>
public sealed class JCSyncRecurringJob(
    JCDataUpdaterService dataUpdaterService,
    IOptions<JCInterfaceOptions> options,
    ILogger<JCSyncRecurringJob> logger
) : IRecurringJob
{
    public string JobName => "jc-interface-sync";

    public string CronSchedule => options.Value.SyncCron;

    public async Task Execute(PerformContext? context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Starting scheduled JC Interface sync");
        await dataUpdaterService.SyncAllAsync();
        logger.LogInformation("Completed scheduled JC Interface sync");
    }
}
