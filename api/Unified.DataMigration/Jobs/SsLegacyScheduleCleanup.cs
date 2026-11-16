using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Unified.DataMigration.Jobs;

// Migration-owned cleanup; do not change unrelated recurring registration or CASS.
public sealed class SsLegacyScheduleCleanup(IServiceProvider services) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        services.GetService<IRecurringJobManager>()?.RemoveIfExists("data-migration:ss");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
