using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Unified.Authorization.Claims;
using Unified.DataMigration.FeatureFlags;
using Unified.DataMigration.Models;
using Unified.DataMigration.Options;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

public sealed class DataMigrationOperations(
    UnifiedDbContext db,
    IOptions<DataMigrationOptions> options,
    IOptions<DataMigrationFeatureFlags> flags,
    IMigrationRunCoordinator controls,
    IMigrationJobDispatcher dispatcher,
    SsRollbackService rollback,
    IHttpContextAccessor httpContext,
    IAuthorizationService authorization
)
{
    public static readonly string[] Jobs = ["MyTeam", "Training", "Scheduling", "Report"];

    public static void Scope(string source, string? job = null)
    {
        if (source == "CASS")
            throw new MigrationOperationException(501, "SourceDeferred");
        if (source != "SS" || job is not null && !Jobs.Contains(job, StringComparer.Ordinal))
            throw new MigrationOperationException(404, "MigrationScopeNotFound");
    }

    private IQueryable<DataMigrationRun> Runs(string source, string? job)
    {
        Scope(source, job);
        var q = db.DataMigrationRuns.AsNoTracking().Where(x => x.Source == source);
        return job is null ? q : q.Where(x => x.Job == job);
    }

    private static MigrationRunDto Map(DataMigrationRun x) =>
        new(
            x.Id,
            x.Source,
            Jobs.Contains(x.Job) || x.Job is "Rollback" or "Legacy" ? x.Job : MigrationDiagnostics.Opaque(x.Job),
            x.Status is "Running" or "Completed" or "Failed" or "NeedsAction" or "Abandoned" or "RolledBack"
                ? x.Status
                : "Unknown",
            x.Status switch
            {
                "Failed" or "Abandoned" => "Fail",
                "NeedsAction" => "NeedsAction",
                // Completion of a loader is not final business acceptance; simulation is never applied lineage.
                _ => "NotAccepted",
            },
            x.DryRun,
            x.MyTeamRunId,
            x.RollbackOfRunId,
            x.CutoverDate,
            x.HistoryStartDate,
            x.StartedOn,
            x.CompletedOn,
            x.HeartbeatOn,
            x.ScannedCount,
            x.InsertedCount,
            x.UpdatedCount,
            x.UnchangedCount,
            x.SkippedCount,
            x.RejectedCount,
            x.FailedCount,
            x.CommittedBatchCount
        );

    public async Task<MigrationRunDto> RunAsync(string source, string? job, Guid id, CancellationToken ct)
    {
        var run =
            await Runs(source, job).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new MigrationOperationException(404, "RunNotFoundInScope");
        return await WithVerdictAsync(run, ct);
    }

    private async Task<MigrationRunDto> WithVerdictAsync(DataMigrationRun run, CancellationToken ct)
    {
        var dto = Map(run);
        if (run.Job != "Report" || run.Status != "Completed")
            return dto;
        var findings = await db
            .DataMigrationOutcomes.AsNoTracking()
            .Where(x => x.RunId == run.Id)
            .Select(x => new
            {
                x.EntityType,
                x.Outcome,
                x.Reason,
            })
            .ToListAsync(ct);
        var verdict =
            run.FailedCount + run.RejectedCount > 0
            || await db.DataMigrationFailures.AnyAsync(
                x =>
                    x.RunId == run.Id
                    && x.Source == run.Source
                    && x.ErrorType != "DispatchFailed"
                    && x.ErrorType != "QueuedExecutionRefused"
                    && x.ErrorType != "ExecutionFailed"
                    && x.ErrorType != "Canceled",
                ct
            )
            || findings.Any(x => x.Outcome is "Failed" or "Rejected")
                ? "Fail"
            : await db.DataMigrationFailures.AnyAsync(x => x.RunId == run.Id && x.Source == run.Source, ct)
                ? "NeedsAction"
            : findings.Any(x => x.Outcome is "NeedsAction" or "DroppedBadData") ? "NeedsAction"
            : run.DryRun ? "NeedsAction"
            : findings.Any(x =>
                x.Outcome is not ("Migrated" or "Merged" or "MigratedWithDifference" or "Excluded" or "Unchanged")
            )
                ? "NeedsAction"
            : !findings.Any(x =>
                x.EntityType == "SourceOracleValidation"
                && x.Outcome == "Migrated"
                && x.Reason == "ActualIndependentOracleRowsMatched"
            )
                ? "NeedsAction"
            : findings.Any(x => x.Outcome == "MigratedWithDifference") ? "PassWithKnownDifferences"
            : "Pass";
        return dto with { Verdict = verdict };
    }

    private static void Page(int offset, int limit)
    {
        if (offset < 0 || limit is < 1 or > 200)
            throw new MigrationOperationException(400, "InvalidPage");
    }

    public async Task<MigrationPage<MigrationRunDto>> RunsAsync(
        string source,
        string? job,
        int offset,
        int limit,
        CancellationToken ct
    )
    {
        Page(offset, limit);
        var q = Runs(source, job);
        var values = new List<MigrationRunDto>();
        foreach (
            var run in await q.OrderByDescending(x => x.StartedOn)
                .ThenBy(x => x.Id)
                .Skip(offset)
                .Take(limit)
                .ToListAsync(ct)
        )
            values.Add(await WithVerdictAsync(run, ct));
        return new(values, offset, limit, await q.CountAsync(ct));
    }

    public async Task<MigrationStatusDto> StatusAsync(string source, string job, CancellationToken ct)
    {
        Scope(source, job);
        var settings = options.Value.Sources.SS;
        var configured = settings.Jobs.For(job);
        async Task<MigrationControlDto> Control(string key)
        {
            var value = await db.DataMigrationControls.AsNoTracking().SingleOrDefaultAsync(x => x.Source == key, ct);
            return new(key, value?.IsPaused ?? false, value?.AbortRequestedOn is not null, value?.ChangedOn);
        }
        return new(
            source,
            job,
            flags.Value.Enabled && settings.Enabled && configured.Enabled,
            configured.DryRun,
            configured.BatchSize,
            settings.CutoverDate,
            settings.HistoryStartDate,
            await Control(source),
            await Control($"{source}:{job}")
        );
    }

    public async Task<MigrationAcceptedDto> TriggerAsync(
        string source,
        string job,
        bool dryRun,
        Guid actor,
        CancellationToken ct
    )
    {
        Scope(source, job);
        if (actor == Guid.Empty)
            throw new MigrationOperationException(403, "ResolvedMigrationActorRequired");
        var user = httpContext.HttpContext?.User;
        if (
            user?.Identity?.IsAuthenticated != true
            || !Guid.TryParse(user.FindFirstValue(UnifiedClaimTypes.UserId), out var resolved)
            || resolved != actor
            || !(await authorization.AuthorizeAsync(user, MigrationPermissions.OperationsPolicy)).Succeeded
        )
            throw new MigrationOperationException(403, "AuthenticatedMigrationRequestRequired");
        try
        {
            dispatcher.Validate(job, dryRun);
        }
        catch (InvalidOperationException)
        {
            throw new MigrationOperationException(409, "JobDisabledOrConfigurationInvalid");
        }
        var settings = options.Value.Sources.SS;
        var run =
            await controls.TryStartJobAsync(job, dryRun, settings.CutoverDate!.Value, ct, settings.HistoryStartDate)
            ?? throw new MigrationOperationException(409, "JobBusyPausedOrLineageNotReady");
        try
        {
            await AuditAsync(source, job, run.Id, actor, "Trigger", ct, dryRun);
            dispatcher.Enqueue(run.Id);
        }
        catch
        {
            db.ChangeTracker.Clear();
            var failed = await db.DataMigrationRuns.SingleAsync(x => x.Id == run.Id, CancellationToken.None);
            MigrationTechnicalFailure.Record(
                db,
                failed,
                "DispatchFailed",
                "Job dispatch failed before business execution."
            );
            await db.SaveChangesAsync(CancellationToken.None);
            throw new MigrationOperationException(503, "JobDispatchFailed");
        }
        return new(run.Id, source, job, dryRun);
    }

    public async Task<MigrationControlDto> ControlAsync(
        string source,
        string? job,
        string action,
        Guid actor,
        CancellationToken ct
    )
    {
        Scope(source, job);
        if (action is not ("pause" or "resume" or "abort"))
            throw new MigrationOperationException(400, "InvalidControlAction");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(872,1)", ct);
        var key = job is null ? source : $"{source}:{job}";
        var value = await db.DataMigrationControls.SingleOrDefaultAsync(x => x.Source == key, ct);
        if (value is null)
        {
            value = new() { Source = key };
            db.DataMigrationControls.Add(value);
        }
        value.IsPaused = action != "resume";
        if (action == "abort")
            value.AbortRequestedOn = DateTimeOffset.UtcNow;
        if (action == "resume")
            value.AbortRequestedOn = null;
        value.ChangedOn = DateTimeOffset.UtcNow;
        value.ChangedBy = actor.ToString("D");
        await AuditAsync(source, job, null, actor, action, ct);
        await tx.CommitAsync(ct);
        return new(key, value.IsPaused, value.AbortRequestedOn is not null, value.ChangedOn);
    }

    public async Task<MigrationRunDto> RecoverAsync(
        string source,
        string job,
        Guid id,
        DateTimeOffset before,
        Guid actor,
        CancellationToken ct
    )
    {
        await RunAsync(source, job, id, ct);
        if (before >= DateTimeOffset.UtcNow)
            throw new MigrationOperationException(400, "RecoveryRequiresPastBoundary");
        await AuditAsync(source, job, id, actor, "RecoverRequested", ct);
        if (!await controls.RecoverStaleRunAsync(id, before.ToUniversalTime(), ct))
            throw new MigrationOperationException(409, "RunNotStaleOrNotRunning");
        await AuditAsync(source, job, id, actor, "Recover", ct);
        return await RunAsync(source, job, id, ct);
    }

    public async Task<MigrationRollbackPreviewDto> PreviewAsync(
        string source,
        string job,
        Guid id,
        Guid actor,
        CancellationToken ct
    )
    {
        await RunAsync(source, job, id, ct);
        await AuditAsync(source, job, id, actor, "RollbackPreview", ct);
        var value = await rollback.PreviewAsync(id, ct);
        return new(
            id,
            value.Digest,
            value.Rows,
            value.Dependents,
            value.Blockers,
            $"/api/data-migration/{source}/jobs/{job}/runs/{id:D}/rollback-impact?digest={value.Digest}"
        );
    }

    public async Task<byte[]> ImpactAsync(
        string source,
        string job,
        Guid id,
        string digest,
        Guid actor,
        CancellationToken ct
    )
    {
        await RunAsync(source, job, id, ct);
        await AuditAsync(source, job, id, actor, "RollbackImpactDownload", ct);
        return await rollback.ReadImpactAsync(id, digest, ct);
    }

    public async Task<MigrationRollbackReceiptDto> RollbackAsync(
        string source,
        string job,
        Guid id,
        MigrationRollbackRequest request,
        Guid actor,
        CancellationToken ct
    )
    {
        await RunAsync(source, job, id, ct);
        if (request.ConfirmDataLoss != id || !ValidDigest(request.Digest))
            throw new MigrationOperationException(409, "RollbackConfirmationMismatch");
        await AuditAsync(source, job, id, actor, "RollbackConfirm", ct);
        return new(id, await rollback.ApplyAsync(id, $"{id:D}:{request.Digest}", ct));
    }

    internal static bool ValidDigest(string? text) => text is { Length: 64 } && text.All(x => char.IsAsciiHexDigit(x));

    public async Task<MigrationPage<MigrationFindingDto>> FindingsAsync(
        string source,
        string job,
        Guid id,
        string kind,
        int offset,
        int limit,
        CancellationToken ct
    )
    {
        await RunAsync(source, job, id, ct);
        Page(offset, limit);
        if (kind == "outcomes")
        {
            var q = db.DataMigrationOutcomes.AsNoTracking().Where(x => x.RunId == id);
            return new(
                (await q.OrderBy(x => x.Id).Skip(offset).Take(limit).ToListAsync(ct))
                    .Select(MigrationDiagnostics.Finding)
                    .ToList(),
                offset,
                limit,
                await q.CountAsync(ct)
            );
        }
        if (kind == "failures")
        {
            var q = db.DataMigrationFailures.AsNoTracking().Where(x => x.RunId == id && x.Source == source);
            return new(
                (await q.OrderBy(x => x.Id).Skip(offset).Take(limit).ToListAsync(ct))
                    .Select(x => MigrationDiagnostics.Failure(x, job == "Report"))
                    .ToList(),
                offset,
                limit,
                await q.CountAsync(ct)
            );
        }
        throw new MigrationOperationException(400, "InvalidFindingKind");
    }

    private async Task AuditAsync(
        string source,
        string? job,
        Guid? id,
        Guid actor,
        string action,
        CancellationToken ct,
        bool? dryRun = null
    )
    {
        db.AuditRecords.Add(
            new AuditRecord
            {
                ActorUserId = actor,
                OccurredOn = DateTimeOffset.UtcNow,
                Action = "Added",
                EntityType = "DataMigrationOperation",
                EntityPK = id?.ToString("D") ?? (job is null ? source : $"{source}:{job}"),
                TableName = "DataMigrationRuns",
                NewValues = JsonSerializer.Serialize(
                    new
                    {
                        Source = source,
                        Job = job,
                        RunId = id,
                        Operation = action,
                        DryRun = dryRun,
                    }
                ),
            }
        );
        await db.SaveChangesAsync(ct);
    }

    // Exports are complete snapshot streams, not a paginated UI response or a fixed row cap.
    // A final End record/count lets clients distinguish a complete file from an interrupted stream.
    public async Task ExportAsync(
        string source,
        string job,
        Guid id,
        string format,
        Guid actor,
        TextWriter writer,
        CancellationToken ct
    )
    {
        var run = await RunAsync(source, job, id, ct);
        if (format is not ("csv" or "markdown"))
            throw new MigrationOperationException(400, "InvalidExportFormat");
        await AuditAsync(source, job, id, actor, "FindingsExport", ct);
        await using var tx = await db.Database.BeginTransactionAsync(
            db.Database.IsNpgsql() ? IsolationLevel.RepeatableRead : IsolationLevel.Serializable,
            ct
        );
        // Refresh run summary inside the SAME export snapshot.
        run = await RunAsync(source, job, id, ct);
        var columns = new[]
        {
            "Kind",
            "Source",
            "Job",
            "RunId",
            "DryRun",
            "RunStatus",
            "Verdict",
            "Id",
            "Entity",
            "Key",
            "Outcome",
            "Code",
            "OccurredOn",
        };
        async Task Line(IEnumerable<string> cells)
        {
            var values = cells.Select(x => format == "csv" ? Csv(x) : Markdown(x));
            await writer.WriteLineAsync(
                (format == "csv" ? string.Join(",", values) : "| " + string.Join(" | ", values) + " |").AsMemory(),
                ct
            );
        }
        await Line(columns);
        if (format == "markdown")
            await Line(columns.Select(_ => "---"));
        async Task Row(
            string kind,
            string rowId,
            string entity,
            string key,
            string outcome,
            string code,
            string date
        ) =>
            await Line([
                kind,
                source,
                job,
                id.ToString("D"),
                run.DryRun.ToString(),
                run.Status,
                run.Verdict,
                rowId,
                entity,
                key,
                outcome,
                code,
                date,
            ]);
        await Row(
            "Run",
            "",
            "",
            "",
            "",
            $"Scanned={run.Scanned};Inserted={run.Inserted};Updated={run.Updated};Skipped={run.Skipped};Rejected={run.Rejected};Failed={run.Failed}",
            ""
        );
        var count = 0;
        await foreach (
            var value in db
                .DataMigrationOutcomes.AsNoTracking()
                .Where(x => x.RunId == id)
                .OrderBy(x => x.Id)
                .AsAsyncEnumerable()
                .WithCancellation(ct)
        )
        {
            var finding = MigrationDiagnostics.Finding(value);
            await Row(
                finding.Kind,
                finding.Id.ToString(CultureInfo.InvariantCulture),
                finding.Entity,
                finding.Key,
                finding.Outcome,
                finding.Code,
                ""
            );
            count++;
        }
        await foreach (
            var value in db
                .DataMigrationFailures.AsNoTracking()
                .Where(x => x.RunId == id && x.Source == source)
                .OrderBy(x => x.Id)
                .AsAsyncEnumerable()
                .WithCancellation(ct)
        )
        {
            var finding = MigrationDiagnostics.Failure(value, job == "Report");
            await Row(
                finding.Kind,
                finding.Id.ToString(CultureInfo.InvariantCulture),
                finding.Entity,
                finding.Key,
                finding.Outcome,
                finding.Code,
                finding.OccurredOn?.ToString("O") ?? ""
            );
            count++;
        }
        await Row("End", count.ToString(CultureInfo.InvariantCulture), "", "", "", "SnapshotComplete", "");
        await tx.RollbackAsync(ct);
    }

    internal static string Csv(string text)
    {
        // Also defend spreadsheet formula injection, even if a future safe column contains a prefix.
        if (text.Length > 0 && "=+-@\t\r".Contains(text[0]))
            text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    internal static string Markdown(string text) =>
        text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\\", "\\\\")
            .Replace("|", "\\|")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("`", "\\`");
}
