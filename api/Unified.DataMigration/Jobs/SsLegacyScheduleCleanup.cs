using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Unified.DataMigration.FeatureFlags;

namespace Unified.DataMigration.Jobs;

// Migration-owned cleanup; do not change unrelated recurring registration or CASS.
public sealed class SsLegacyScheduleCleanup(IServiceProvider services, IOptions<DataMigrationFeatureFlags> flags)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var manager = services.GetService<IRecurringJobManager>();
        manager?.RemoveIfExists("data-migration:ss");
        if (!flags.Value.Enabled)
            foreach (var job in new[] { "myteam", "training", "scheduling", "report" })
                manager?.RemoveIfExists($"data-migration:ss:{job}");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
