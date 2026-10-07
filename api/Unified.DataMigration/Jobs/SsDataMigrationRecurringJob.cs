using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Options;
using Unified.Common.Jobs;
using Unified.DataMigration.FeatureFlags;
using Unified.DataMigration.Options;
using Unified.DataMigration.Services;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Jobs;

public sealed class SsDataMigrationRecurringJob(
    IOptions<DataMigrationFeatureFlags> featureFlags,
    IOptions<DataMigrationOptions> options,
    DataMigrationControlService controlService,
    DataMigrationOrchestrator orchestrator
) : IRecurringJob
{
    public string JobName => "data-migration:ss";

    public string CronSchedule => string.Empty;

    [DisableConcurrentExecution(600)]
    [AutomaticRetry(Attempts = 0)]
    public async Task Execute(PerformContext? context, CancellationToken cancellationToken)
    {
        if (!featureFlags.Value.Enabled || !options.Value.Sources.SS.Enabled)
        {
            throw new InvalidOperationException("SS migration is disabled.");
        }

        DataMigrationRun? run;
        try
        {
            run = await controlService.TryStartRunAsync("SS", options.Value.Sources.SS.DryRun, cancellationToken);
            if (run is not null)
            {
                await orchestrator.RecordFailureAsync(
                    new DataMigrationFailure
                    {
                        RunId = run.Id,
                        Source = "SS",
                        ErrorType = "MappingApprovalRequired",
                        ErrorMessage =
                            "No SS mapping contracts are approved. No source rows were read or target rows migrated.",
                        OccurredOn = DateTimeOffset.UtcNow,
                    },
                    CancellationToken.None
                );
                await controlService.CompleteRunAsync(run, "Failed", CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("SS migration was canceled.", cancellationToken);
        }
        catch (Exception)
        {
            throw new InvalidOperationException("SS migration failed. Verify migration state storage.");
        }

        if (run is null)
        {
            throw new InvalidOperationException("SS migration could not acquire the durable run guard.");
        }

        throw new InvalidOperationException("SS migration requires human approval of mapping contracts.");
    }
}
