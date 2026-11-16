using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Options;
using Unified.Common.Jobs;
using Unified.DataMigration.FeatureFlags;
using Unified.DataMigration.Options;
using Unified.DataMigration.Services;

namespace Unified.DataMigration.Jobs;

public abstract class SsMigrationRecurringJob(
    string job,
    IOptions<DataMigrationFeatureFlags> flags,
    IOptions<DataMigrationOptions> options,
    SsMigrationExecutor executor
) : IRecurringJob
{
    public string JobName => $"data-migration:ss:{job.ToLowerInvariant()}";
    public string CronSchedule =>
        flags.Value.Enabled && options.Value.Sources.SS.Enabled && options.Value.Sources.SS.Jobs.For(job).Enabled
            ? options.Value.Sources.SS.Jobs.For(job).CronSchedule ?? ""
            : "";

    [AutomaticRetry(Attempts = 0)]
    public Task Execute(PerformContext? context, CancellationToken cancellationToken) =>
        executor.ExecuteAsync(job, cancellationToken);
}

public sealed class SsDataMigrationRecurringJob(
    IOptions<DataMigrationFeatureFlags> flags,
    IOptions<DataMigrationOptions> options,
    SsMigrationExecutor executor
) : SsMigrationRecurringJob("MyTeam", flags, options, executor);

public sealed class SsTrainingRecurringJob(
    IOptions<DataMigrationFeatureFlags> flags,
    IOptions<DataMigrationOptions> options,
    SsMigrationExecutor executor
) : SsMigrationRecurringJob("Training", flags, options, executor);

public sealed class SsSchedulingRecurringJob(
    IOptions<DataMigrationFeatureFlags> flags,
    IOptions<DataMigrationOptions> options,
    SsMigrationExecutor executor
) : SsMigrationRecurringJob("Scheduling", flags, options, executor);

public sealed class SsReportRecurringJob(
    IOptions<DataMigrationFeatureFlags> flags,
    IOptions<DataMigrationOptions> options,
    SsMigrationExecutor executor
) : SsMigrationRecurringJob("Report", flags, options, executor);
