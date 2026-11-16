using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Unified.DataMigration.FeatureFlags;
using Unified.DataMigration.Options;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

public sealed class SsMigrationExecutor(
    UnifiedDbContext db,
    IConfiguration configuration,
    IOptions<DataMigrationOptions> options,
    IOptions<DataMigrationFeatureFlags> flags,
    DataMigrationControlService controls,
    SsSnapshotReader reader,
    SsMyTeamWriter myTeam,
    SsTrainingWriter training,
    SsSchedulingWriter scheduling,
    IEnumerable<ISsMigrationBusinessValidator>? businessValidators = null
)
{
    public async Task ExecuteAsync(string job, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            throw new OperationCanceledException("SS migration was canceled.", ct);
        var source = options.Value.Sources.SS;
        var settings = source.Jobs.For(job);
        if (!flags.Value.Enabled || !source.Enabled || !settings.Enabled)
            throw new InvalidOperationException("SS migration job is disabled.");
        // Inspect raw configuration: a code default of false is not explicit operator consent.
        foreach (var key in new[] { "ExpireRegions", "ExpireLocations", "ExpireCourtRooms" })
            if (!bool.TryParse(configuration[$"JCInterface:{key}"], out var value) || value)
                throw new InvalidOperationException("SS migration requires explicit false JC expiry flags.");
        if (source.CutoverDate != new DateOnly(2026, 12, 1))
            throw new InvalidOperationException("SS CutoverDate must explicitly be 2026-12-01.");
        if (
            !settings.DryRun
            && (
                !source.AllowUnrecoverableSyntheticApply
                || db.Database.GetDbConnection().Database
                    is not (
                        "unified_ss_rehearsal"
                        or "unified_ss_rehearsal_ready"
                        or "unified_ss_rehearsal_ready_v2"
                        or "unified_ss_rehearsal_ready_v3"
                        or "unified_ss_rehearsal_ready_v3_drift"
                        or "unified_ss_rehearsal_ready_v4"
                    )
            )
        )
            throw new InvalidOperationException(
                "SS apply is disabled outside explicitly acknowledged isolated synthetic rehearsals until safe rollback is implemented."
            );
        DataMigrationRun? acquired;
        try
        {
            acquired = await controls.TryStartJobAsync(job, settings.DryRun, source.CutoverDate.Value, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException("SS migration was canceled.", ct);
        }
        catch
        {
            // Connection/provider errors before acquisition must not leak credentials either.
            throw new InvalidOperationException("SS migration failed. Verify migration state storage.");
        }
        var run =
            acquired
            ?? throw new InvalidOperationException("SS job is paused, already running, or its lineage is not ready.");
        var session = new SsWriteSession(db, run);
        try
        {
            if (job == "Report")
            {
                var reportRows = await reader.ReadAsync(
                    SsMyTeamWriter
                        .Tables.Concat(SsTrainingWriter.Tables)
                        .Concat(SsSchedulingWriter.Tables)
                        .Distinct()
                        .ToArray(),
                    settings.BatchSize,
                    ct
                );
                await using var reportTx = await db.Database.BeginTransactionAsync(ct);
                await FenceAsync(run, ct);
                await new SsReconciliationWriter(db).WriteAsync(reportRows, session, ct);
                foreach (var exclusion in await reader.ReadExcludedCountsAsync(ct))
                    session.Outcome(
                        "ExcludedTableMetric",
                        exclusion.Key,
                        "Excluded",
                        $"ExcludedSystemOrPermissionRows:{exclusion.Value}"
                    );
                foreach (var validator in businessValidators ?? [])
                    await validator.ValidateAsync(job, reportRows, session, ct);
                // Independent source-service captures are mandatory. Source SQL/row counts are not
                // substituted for O1..O5 service results, even on a nominally ready fixture.
                if (!session.Outcomes.Any(x => x.EntityType == "SourceOracleValidation"))
                    session.Outcome(
                        "SourceOracleValidation",
                        run.Id.ToString(),
                        "NeedsAction",
                        "IndependentO1ToO5CaptureComparisonRequired"
                    );
                await PersistAsync(run, session, ct);
                await reportTx.CommitAsync(ct);
                return;
            }
            if (settings.DryRun && job != "MyTeam")
            {
                // A dry-run parent rolled back its real targets; never borrow live crosswalks as fake candidates.
                session.Outcome("DryRunLineage", run.Id.ToString(), "Rejected", "MissingIsolatedParentCandidates");
                await PersistAsync(run, session, ct);
                return;
            }
            if (settings.DryRun && await db.DataMigrationRecords.AnyAsync(x => x.Source == "SS", ct))
            {
                session.Outcome(
                    "DryRunLineage",
                    run.Id.ToString(),
                    "Rejected",
                    "DryRunRequiresIsolatedTargetCandidates"
                );
                await PersistAsync(run, session, ct);
                return;
            }
            var tables = job switch
            {
                "MyTeam" => SsMyTeamWriter.Tables,
                "Training" => SsTrainingWriter.Tables,
                "Scheduling" => SsSchedulingWriter.Tables,
                _ => throw new ArgumentOutOfRangeException(nameof(job)),
            };
            var rows = await reader.ReadAsync(tables, settings.BatchSize, ct);
            var boundary = new DateTimeOffset(
                TimeZoneInfo.ConvertTimeToUtc(
                    source.HistoryStartDate.ToDateTime(TimeOnly.MinValue),
                    TimeZoneInfo.FindSystemTimeZoneById("America/Vancouver")
                )
            );
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await FenceAsync(run, ct);
            if (job == "MyTeam")
                await myTeam.WriteAsync(rows, session, boundary, ct);
            if (job == "Training")
                await training.WriteAsync(rows, session, boundary, ct);
            if (job == "Scheduling")
            {
                await scheduling.WriteAsync(rows, session, boundary, ct);
                var validators = (businessValidators ?? []).ToList();
                if (validators.Count == 0)
                    session.Outcome(
                        "SchedulingValidation",
                        run.Id.ToString(),
                        "NeedsAction",
                        "ActualSchedulingReadValidatorUnavailable"
                    );
                foreach (var validator in validators)
                    await validator.ValidateAsync(job, rows, session, ct);
            }
            if (settings.DryRun)
            {
                // Exactly the same EF DML and PostgreSQL constraints, not a bypassed writer.
                await db.SaveChangesAsync(ct);
                await tx.RollbackAsync(ct);
                db.ChangeTracker.Clear();
            }
            else
            {
                await PersistAsync(run, session, ct);
                await tx.CommitAsync(ct);
                return;
            }
            await PersistAsync(run, session, ct);
        }
        catch (Exception ex)
        {
            db.ChangeTracker.Clear();
            var persisted = await db.DataMigrationRuns.SingleAsync(x => x.Id == run.Id, CancellationToken.None);
            if (persisted.Status != "Running")
                throw new InvalidOperationException("SS migration stopped after its run fence was revoked.");
            persisted.Status = "Failed";
            persisted.CompletedOn = DateTimeOffset.UtcNow;
            persisted.FailedCount++;
            db.DataMigrationFailures.Add(
                new DataMigrationFailure
                {
                    RunId = run.Id,
                    Source = "SS",
                    ErrorType = ex is OperationCanceledException ? "Canceled" : "ExecutionFailed",
                    ErrorMessage =
                        "SS job failed; business transaction rolled back. Inspect safe source outcomes and configuration.",
                    OccurredOn = DateTimeOffset.UtcNow,
                }
            );
            await db.SaveChangesAsync(CancellationToken.None);
            throw new InvalidOperationException("SS migration failed; inspect migration-only run evidence.");
        }
    }

    private async Task FenceAsync(DataMigrationRun run, CancellationToken ct)
    {
        // Recheck AFTER acquiring the durable row lock: recovery may have revoked
        // the run while its source snapshot was loading. Report needs the same fence.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"DataMigrationRuns\" WHERE \"Id\" = {run.Id} FOR UPDATE",
            ct
        );
        var fenced = await db.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == run.Id, ct);
        if (fenced.Status != "Running")
            throw new InvalidOperationException("SS run fence was revoked.");
        var keys = new[] { "SS", $"SS:{run.Job}" };
        if (
            await db.DataMigrationControls.AnyAsync(
                x => keys.Contains(x.Source) && (x.IsPaused || x.AbortRequestedOn != null),
                ct
            )
        )
            throw new InvalidOperationException("SS job was stopped before business writes.");
    }

    private async Task PersistAsync(DataMigrationRun run, SsWriteSession session, CancellationToken ct)
    {
        var persisted = await db.DataMigrationRuns.SingleAsync(x => x.Id == run.Id, ct);
        db.DataMigrationOutcomes.AddRange(session.Outcomes);
        persisted.ScannedCount = session.SourceOutcomes.Count();
        persisted.InsertedCount = session.Inserted;
        persisted.UnchangedCount = session.Outcomes.Count(x =>
            x.Reason is "ExistingCrosswalk" or "ExistingGroupCrosswalk"
        );
        persisted.SkippedCount = session.SourceOutcomes.Count(x => x.Outcome == "Excluded");
        persisted.RejectedCount = session.Outcomes.Count(x => x.Outcome == "Rejected");
        persisted.FailedCount = session.Outcomes.Count(x => x.Outcome == "Failed");
        persisted.Status =
            persisted.FailedCount + persisted.RejectedCount > 0 ? "Failed"
            : session.Outcomes.Any(x => x.Outcome is "NeedsAction" or "DroppedBadData") ? "NeedsAction"
            : "Completed";
        persisted.CompletedOn = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
