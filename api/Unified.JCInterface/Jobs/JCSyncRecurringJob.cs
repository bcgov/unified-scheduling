using Hangfire.Server;
using Microsoft.Extensions.Options;
using Unified.Common.Jobs;
using Unified.JCInterface.Options;
using Unified.JCInterface.Services;

namespace Unified.JCInterface.Jobs;

public sealed class JCSyncRecurringJob(
    JCDataUpdaterService dataUpdaterService,
    IOptions<JCInterfaceOptions> options
) : IRecurringJob
{
    public string JobName => "jc-interface-sync";

    public string CronSchedule => options.Value.SyncCron;

    public async Task Execute(PerformContext? context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await dataUpdaterService.SyncAllAsync();
    }
}