using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using Unified.DataMigration.FeatureFlags;
using Unified.DataMigration.Options;
using Unified.DataMigration.Services.Bulk;
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
    IEnumerable<ISsMigrationBusinessValidator>? businessValidators = null,
    SsRollbackJournal? rollbackJournal = null,
    SsTrainingBulkWriter? trainingBulk = null,
    SsSchedulingBulkWriter? schedulingBulk = null
)
{
    public Task ExecuteAsync(string job, CancellationToken ct)
    {
        ConfigureMigrationCommandTimeout();
        return ExecuteCoreAsync(job, null, ct);
    }

    [Hangfire.AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAcquiredAsync(Guid runId, CancellationToken ct)
    {
        ConfigureMigrationCommandTimeout();
        await db.Database.OpenConnectionAsync(ct);
        var locked = false;
        try
        {
            // Hangfire is at-least-once. A session lock for THIS stable run prevents
            // duplicate deliveries without serializing Training against Scheduling.
            locked =
                await db
                    .Database.SqlQuery<int>(
                        $"SELECT CASE WHEN pg_try_advisory_lock(873,hashtext({runId.ToString("D")})) THEN 1 ELSE 0 END AS \"Value\""
                    )
                    .SingleAsync(ct) == 1;
            if (!locked)
                throw new InvalidOperationException("SS run already has an execution owner.");
            await ExecuteOwnedAsync(runId, ct);
        }
        finally
        {
            try
            {
                if (locked)
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT pg_advisory_unlock(873,hashtext({runId.ToString("D")}))",
                        CancellationToken.None
                    );
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }

    private void ConfigureMigrationCommandTimeout()
    {
        var connectionString = configuration["DatabaseConnectionString"];
        var builder = string.IsNullOrWhiteSpace(connectionString)
            ? null
            : new NpgsqlConnectionStringBuilder(connectionString);
        var timeout =
            builder is not null && builder.ShouldSerialize("Command Timeout")
                ? builder.CommandTimeout
                : options.Value.CommandTimeoutSeconds;
        db.Database.SetCommandTimeout(timeout);
    }

    private async Task ExecuteOwnedAsync(Guid runId, CancellationToken ct)
    {
        var run = await db
            .DataMigrationRuns.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == runId && x.Source == "SS", ct);
        if (run?.Status != "Running")
            throw new InvalidOperationException("SS queued run is no longer executable.");
        // The acquired run, never mutable current defaults, determines its simulation mode.
        // Rechecking configuration can disable execution, never escalate a dry request to apply.
        try
        {
            await ExecuteCoreAsync(run.Job, run, ct);
        }
        catch
        {
            db.ChangeTracker.Clear();
            var current = await db.DataMigrationRuns.SingleAsync(x => x.Id == runId, CancellationToken.None);
            if (current.Status == "Running")
            {
                MigrationTechnicalFailure.Record(
                    db,
                    current,
                    "QueuedExecutionRefused",
                    "Queued execution refused; inspect migration configuration and controls."
                );
                await db.SaveChangesAsync(CancellationToken.None);
            }
            throw new InvalidOperationException("SS queued execution failed; inspect migration-only evidence.");
        }
    }

    public MigrationJobOptions ValidateRequest(string job, bool? dryRun = null)
    {
        var source = options.Value.Sources.SS;
        var configured = source.Jobs.For(job);
        var settings = new MigrationJobOptions
        {
            Enabled = configured.Enabled,
            DryRun = dryRun ?? configured.DryRun,
            AllowMigrationApply = configured.AllowMigrationApply,
            BatchSize = configured.BatchSize,
            CronSchedule = configured.CronSchedule,
            PersistenceMode = configured.PersistenceMode,
            BulkChunkSize = configured.BulkChunkSize,
        };
        if (!flags.Value.Enabled || !source.Enabled || !settings.Enabled)
            throw new InvalidOperationException("SS migration job is disabled.");
        if (!new DataMigrationOptionsValidator().Validate(null, options.Value).Succeeded)
            throw new InvalidOperationException("SS migration configuration is invalid.");
        if (!settings.DryRun && !configured.AllowMigrationApply)
            throw new InvalidOperationException("SS apply is disabled for this job.");
        // Inspect raw configuration: a code default of false is not explicit operator consent.
        foreach (var key in new[] { "ExpireRegions", "ExpireLocations", "ExpireCourtRooms" })
            if (!bool.TryParse(configuration[$"JCInterface:{key}"], out var value) || value)
                throw new InvalidOperationException("SS migration requires explicit false JC expiry flags.");
        if (source.CutoverDate != new DateOnly(2026, 12, 1))
            throw new InvalidOperationException("SS CutoverDate must explicitly be 2026-12-01.");
        return settings;
    }

    internal async Task ValidateExecutionAsync(string job, DataMigrationRun? queuedRun, CancellationToken ct)
    {
        var source = options.Value.Sources.SS;
        var settings = ValidateRequest(job, queuedRun?.DryRun);
        if (
            queuedRun is not null
            && (queuedRun.Source != "SS" || queuedRun.Job != job || queuedRun.Status != "Running")
        )
            throw new InvalidOperationException("SS queued run is no longer executable.");
        if (queuedRun is null && !settings.DryRun)
            throw new InvalidOperationException("SS apply requires an audited API-acquired run.");
        if (
            queuedRun is not null
            && (queuedRun.CutoverDate != source.CutoverDate || queuedRun.HistoryStartDate != source.HistoryStartDate)
        )
            throw new InvalidOperationException("SS queued run configuration changed.");
        if (queuedRun is not null && !queuedRun.DryRun)
        {
            var evidence = await db
                .AuditRecords.AsNoTracking()
                .Where(x =>
                    x.EntityType == "DataMigrationOperation"
                    && x.TableName == "DataMigrationRuns"
                    && x.Action == "Added"
                    && x.EntityPK == queuedRun.Id.ToString("D")
                    && x.ActorUserId != null
                    && x.ActorUserId != Guid.Empty
                )
                .Select(x => x.NewValues)
                .ToListAsync(ct);
            if (!evidence.Any(x => IsTriggerEvidence(x, queuedRun)))
                throw new InvalidOperationException("SS apply requires an audited API-acquired run.");
            // Persistence mode is bound at trigger time; a later configuration change refuses the run.
            if (!evidence.Any(x => IsTriggerEvidence(x, queuedRun, settings.PersistenceMode)))
                throw new InvalidOperationException("SS queued run configuration changed.");
        }
    }

    internal static bool IsTriggerEvidence(string? json, DataMigrationRun run, MigrationPersistenceMode? mode = null)
    {
        try
        {
            using var document = JsonDocument.Parse(json ?? "");
            var root = document.RootElement;
            if (mode is not null)
            {
                // Evidence written before PersistenceMode existed means the default PerRow.
                var recorded = root.TryGetProperty("PersistenceMode", out var value)
                    ? value.GetString()
                    : nameof(MigrationPersistenceMode.PerRow);
                if (recorded != mode.ToString())
                    return false;
            }
            return root.GetProperty("Source").GetString() == "SS"
                && root.GetProperty("Job").GetString() == run.Job
                && root.GetProperty("RunId").GetGuid() == run.Id
                && root.GetProperty("Operation").GetString() == "Trigger"
                && root.GetProperty("DryRun").ValueKind == JsonValueKind.False;
        }
        catch (Exception ex)
            when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            return false;
        }
    }

    private async Task ExecuteCoreAsync(string job, DataMigrationRun? queuedRun, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            throw new OperationCanceledException("SS migration was canceled.", ct);
        var source = options.Value.Sources.SS;
        await ValidateExecutionAsync(job, queuedRun, ct);
        var settings = ValidateRequest(job, queuedRun?.DryRun);
        DataMigrationRun? acquired = queuedRun;
        try
        {
            acquired ??= await controls.TryStartJobAsync(
                job,
                settings.DryRun,
                source.CutoverDate!.Value,
                ct,
                source.HistoryStartDate
            );
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
            if (settings.DryRun)
            {
                var result = await new SsDryRunSimulation(
                    db,
                    configuration,
                    reader,
                    myTeam,
                    training,
                    scheduling,
                    businessValidators ?? []
                ).ExecuteAsync(run, session, source, settings.BatchSize, token => FenceAsync(run, token), ct);
                session = result.Session;
                await using var simulationReceiptTx = await db.Database.BeginTransactionAsync(ct);
                await FenceAsync(run, ct);
                await PersistAsync(run, session, ct);
                var completed = await db.DataMigrationRuns.SingleAsync(x => x.Id == run.Id, ct);
                if (completed.Status == "Completed" && result.Manifest is not null)
                {
                    db.DataMigrationSimulations.Add(result.Manifest);
                    await db.SaveChangesAsync(ct);
                }
                await simulationReceiptTx.CommitAsync(ct);
                return;
            }
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
                var stream = SsBatchProgress.Stream(source.HistoryStartDate);
                var pendingRetries = await db.DataMigrationWorkItems.CountAsync(
                    x => x.Source == "SS" && x.Stream == stream && x.PendingRetry,
                    ct
                );
                if (pendingRetries > 0)
                    session.Outcome(
                        "RetryQueueValidation",
                        run.Id.ToString(),
                        "NeedsAction",
                        "UnresolvedPrimaryRetryItems"
                    );
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
            var tables = job switch
            {
                "MyTeam" => SsMyTeamWriter.Tables,
                "Training" => SsTrainingWriter.Tables,
                "Scheduling" => SsSchedulingWriter.Tables,
                _ => throw new ArgumentOutOfRangeException(nameof(job)),
            };
            var boundary = new DateTimeOffset(
                TimeZoneInfo.ConvertTimeToUtc(
                    source.HistoryStartDate.ToDateTime(TimeOnly.MinValue),
                    TimeZoneInfo.FindSystemTimeZoneById("America/Vancouver")
                )
            );
            var rows = await reader.ReadAsync(tables, settings.BatchSize, ct, job == "MyTeam" ? boundary : null);
            if (job == "Training" && settings.PersistenceMode == MigrationPersistenceMode.Bulk)
            {
                // Same acquisition, lineage, run lock, fence and counters; no rollback journal is
                // captured (undo is crosswalk-scoped, see SsBulkUndo).
                await (
                    trainingBulk ?? throw new InvalidOperationException("SS Training bulk writer is not registered.")
                ).ExecuteAsync(
                    run,
                    rows,
                    boundary,
                    source.HistoryStartDate,
                    settings.BulkChunkSize,
                    token => FenceAsync(run, token, captureJournal: false),
                    ct
                );
                return;
            }
            if (job == "Scheduling" && settings.PersistenceMode == MigrationPersistenceMode.Bulk)
            {
                // Same rules as PerRow over an in-memory target image; same acquisition, lineage,
                // run lock, fence and counters; no rollback journal (undo: SsBulkUndo).
                await (
                    schedulingBulk
                    ?? throw new InvalidOperationException("SS Scheduling bulk writer is not registered.")
                ).ExecuteAsync(
                    run,
                    rows,
                    boundary,
                    source.HistoryStartDate,
                    settings.BatchSize,
                    settings.BulkChunkSize,
                    (businessValidators ?? []).Any(),
                    token => FenceAsync(run, token, captureJournal: false),
                    ct
                );
                return;
            }
            await using var batches = new SsBatchProgress(
                db,
                session,
                rows,
                source.HistoryStartDate,
                settings.BatchSize,
                token => FenceAsync(run, token)
            );
            await batches.BeginAsync(ct);
            await WriteJobAsync(job, rows, session, boundary, ct);
            await batches.CommitAsync(true, ct);
            return;
        }
        catch (Exception ex)
        {
            if (
                db.Database.GetDbConnection().Database
                    is "unified_ss_rehearsal_repartition_v4"
                        or "unified_ss_rehearsal_simulation_v6"
                && configuration.GetValue<bool>("DataMigrationRehearsal:SafeStack")
            )
            {
                Console.WriteLine("SafeFailureType=" + ex.GetType().Name);
                if (ex is Npgsql.PostgresException pg)
                    Console.WriteLine(
                        "SafeSqlState="
                            + pg.SqlState
                            + ";Position="
                            + pg.Position
                            + ";Table="
                            + pg.TableName
                            + (pg.MessageText == "Simulation schema clone failed" ? ";Phase=" + pg.Hint : "")
                    );
                foreach (
                    var frame in new System.Diagnostics.StackTrace(ex, true)
                        .GetFrames()
                        .Where(x =>
                            x.GetMethod()?.DeclaringType?.Namespace?.StartsWith("Unified.DataMigration") == true
                        )
                        .Take(8)
                )
                    Console.WriteLine(
                        $"SafeFrame={frame.GetMethod()?.DeclaringType?.Name}.{frame.GetMethod()?.Name}:{frame.GetFileLineNumber()}"
                    );
            }
            db.ChangeTracker.Clear();
            var persisted = await db.DataMigrationRuns.SingleAsync(x => x.Id == run.Id, CancellationToken.None);
            if (persisted.Status != "Running")
                throw new InvalidOperationException("SS migration stopped after its run fence was revoked.");
            if (job == "Report" && session.Outcomes.Count > 0)
            {
                // The report transaction rolled back, but already discovered validation failures
                // must not be hidden by a later technical error.
                foreach (var outcome in session.Outcomes)
                    await SsBatchProgress.UpsertOutcomeAsync(db, outcome, CancellationToken.None);
                persisted.FailedCount = session.Outcomes.Count(x => x.Outcome == "Failed");
                persisted.RejectedCount = session.Outcomes.Count(x => x.Outcome == "Rejected");
            }
            MigrationTechnicalFailure.Record(
                db,
                persisted,
                ex is OperationCanceledException ? "Canceled" : "ExecutionFailed",
                job == "Report"
                    ? "SS report did not complete; inspect migration-only evidence."
                    : "SS job failed; current batch rolled back. Earlier committed batches and retry/progress evidence are retained."
            );
            await db.SaveChangesAsync(CancellationToken.None);
            throw new InvalidOperationException("SS migration failed; inspect migration-only run evidence.");
        }
    }

    private async Task WriteJobAsync(
        string job,
        Dictionary<string, List<SsRow>> rows,
        SsWriteSession session,
        DateTimeOffset boundary,
        CancellationToken ct
    )
    {
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
                    session.Run.Id.ToString(),
                    "NeedsAction",
                    "ActualSchedulingReadValidatorUnavailable"
                );
            foreach (var validator in validators)
                await validator.ValidateAsync(job, rows, session, ct);
        }
    }

    private async Task FenceAsync(DataMigrationRun run, CancellationToken ct, bool captureJournal = true)
    {
        // Recheck AFTER acquiring the durable row lock: recovery may have revoked
        // the run while its source snapshot was loading. Report needs the same fence.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM public.\"DataMigrationRuns\" WHERE \"Id\" = {run.Id} FOR UPDATE",
            ct
        );
        var fenced = await db
            .DataMigrationRuns.FromSqlInterpolated($"SELECT * FROM public.\"DataMigrationRuns\" WHERE \"Id\"={run.Id}")
            .AsNoTracking()
            .SingleAsync(ct);
        if (fenced.Status != "Running")
            throw new InvalidOperationException("SS run fence was revoked.");
        var keys = new[] { "SS", $"SS:{run.Job}" };
        if (
            await db
                .DataMigrationControls.FromSqlRaw("SELECT * FROM public.\"DataMigrationControls\"")
                .AsNoTracking()
                .AnyAsync(x => keys.Contains(x.Source) && (x.IsPaused || x.AbortRequestedOn != null), ct)
        )
            throw new InvalidOperationException("SS job was stopped before business writes.");
        if (captureJournal && rollbackJournal is not null)
            await rollbackJournal.CaptureAsync(run, ct);
    }

    private async Task PersistAsync(DataMigrationRun run, SsWriteSession session, CancellationToken ct)
    {
        var persisted = await db.DataMigrationRuns.SingleAsync(x => x.Id == run.Id, ct);
        foreach (var outcome in session.Outcomes)
            await SsBatchProgress.UpsertOutcomeAsync(db, outcome, ct);
        persisted.ScannedCount = session.SourceOutcomes.Count();
        persisted.InsertedCount = session.Inserted;
        persisted.UpdatedCount = session.Updated;
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
