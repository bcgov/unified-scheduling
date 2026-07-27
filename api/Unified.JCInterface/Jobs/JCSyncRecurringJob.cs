using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Hangfire.Server;
using Unified.Hangfire.Jobs;
using Unified.JCInterface.Options;
using Unified.JCInterface.Services;

namespace Unified.JCInterface.Jobs;

/// <summary>
/// Hangfire recurring job that runs the full JC Interface sync pipeline
/// (regions, then locations, then court rooms) via <see cref="JCDataUpdaterService.SyncAllAsync"/>.
/// Schedule is configurable via <see cref="JCInterfaceOptions.SyncCronSchedule"/>.
///
/// The <paramref name="context"/> parameter (auto-injected by Hangfire) isn't used directly here -
/// a global server filter (<c>HangfireConsoleLoggingFilter</c>) already mirrors every <c>ILogger</c>
/// call made during this method (including inside <see cref="JCDataUpdaterService"/>) to the job's
/// Console tab in the dashboard, so plain <c>ILogger</c> calls are sufficient.
/// </summary>
public sealed class JCSyncRecurringJob(
    JCDataUpdaterService jcDataUpdaterService,
    IOptions<JCInterfaceOptions> jcInterfaceOptions,
    ILogger<JCSyncRecurringJob> logger
) : IRecurringJob
{
    public string JobName => nameof(JCSyncRecurringJob);

    public string CronSchedule => jcInterfaceOptions.Value.SyncCronSchedule;

    public async Task Execute(PerformContext? context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting scheduled JC Interface sync");

        await jcDataUpdaterService.SyncAllAsync();

        logger.LogInformation("Completed scheduled JC Interface sync");
    }
}
