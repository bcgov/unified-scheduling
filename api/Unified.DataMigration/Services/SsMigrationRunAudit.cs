using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

// A terminal transition and its explicit summary are saved under the same run-row
// fence and transaction. Audit.NET suppression never suppresses this explicit insert.
internal static class SsMigrationRunAudit
{
    internal const string EntityType = "DataMigrationRunSummary";

    internal static async Task RememberSourceAsync(
        UnifiedDbContext db,
        Guid runId,
        Dictionary<string, List<SsRow>> snapshot,
        Func<CancellationToken, Task> fence,
        CancellationToken ct
    )
    {
        var digest = SsMigrationFingerprints.Source(snapshot, ct);
        var value = SsMigrationFingerprints.SourcePrefix + digest.Value;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await fence(ct);
        var run = await db.DataMigrationRuns.SingleAsync(x => x.Id == runId, ct);
        await db.Entry(run).ReloadAsync(ct);
        if (run.AuditSourceFingerprint is null)
        {
            run.AuditSourceFingerprint = value;
            run.AuditSourceRowCount = digest.Rows;
        }
        else if (run.AuditSourceFingerprint != value || run.AuditSourceRowCount != digest.Rows)
        {
            // A redelivery can read a different snapshot after earlier batches committed.
            // Do not pretend either snapshot describes the entire multi-attempt run.
            run.AuditSourceFingerprint = SsMigrationFingerprints.MultipleSnapshots;
            run.AuditSourceRowCount = null;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        // The worker's original run object may later be attached by the rollback
        // journal; do not leave a second instance of the same key tracked here.
        db.Entry(run).State = EntityState.Detached;
    }

    internal static async Task<bool> FailAsync(
        UnifiedDbContext db,
        Guid runId,
        string errorType,
        string message,
        IReadOnlyList<DataMigrationOutcome>? reportOutcomes = null
    )
    {
        db.ChangeTracker.Clear();
        using var scope = new SsMigrationAuditScope(db);
        await using var tx = await db.Database.BeginTransactionAsync(CancellationToken.None);
        await LockRunAsync(db, runId, CancellationToken.None);
        var run = await db.DataMigrationRuns.SingleAsync(x => x.Id == runId, CancellationToken.None);
        if (run.Status != "Running")
            return false;
        if (run.Job == "Report" && reportOutcomes is { Count: > 0 })
        {
            foreach (var outcome in reportOutcomes)
                await SsBatchProgress.UpsertOutcomeAsync(db, outcome, CancellationToken.None);
            run.FailedCount = reportOutcomes.Count(x => x.Outcome == "Failed");
            run.RejectedCount = reportOutcomes.Count(x => x.Outcome == "Rejected");
        }
        MigrationTechnicalFailure.Record(db, run, errorType, message);
        await db.SaveChangesAsync(CancellationToken.None);
        await WriteAsync(db, runId, CancellationToken.None);
        await tx.CommitAsync(CancellationToken.None);
        return true;
    }

    internal static Task<int> LockRunAsync(UnifiedDbContext db, Guid runId, CancellationToken ct) =>
        db.Database.IsNpgsql()
            ? db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM public.\"DataMigrationRuns\" WHERE \"Id\" = {runId} FOR UPDATE",
                ct
            )
            : Task.FromResult(0);

    internal static async Task WriteAsync(UnifiedDbContext db, Guid runId, CancellationToken ct)
    {
        if (db.Database.IsRelational() && db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("SS run summary requires the terminal transaction.");
        var run = await db.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == runId, ct);
        if (run.Source != "SS" || run.Status is not ("Completed" or "Failed" or "NeedsAction" or "Abandoned"))
            throw new InvalidOperationException("SS run summary requires a terminal migration run.");
        if (run.CompletedOn is null)
            throw new InvalidOperationException("SS run summary requires a completion timestamp.");
        var key = runId.ToString("D");
        if (await db.AuditRecords.AnyAsync(x => x.EntityType == EntityType && x.EntityPK == key, ct))
            throw new InvalidOperationException("SS run already has a terminal summary.");

        var trigger = await FindTriggerAsync(db, run, ct);
        var actor = run.InitiatedByUserId ?? trigger?.Actor;
        var kind =
            run.InitiationKind is "Operator" or "Scheduled" ? run.InitiationKind
            : trigger is not null ? "Operator"
            : "Unknown";
        var mode = run.PersistenceMode ?? trigger?.Mode;
        if (mode is not ("PerRow" or "Bulk"))
            mode = "Unknown";

        var source = run.AuditSourceFingerprint;
        var sourceAvailable =
            source is not null
            && source.StartsWith(SsMigrationFingerprints.SourcePrefix, StringComparison.Ordinal)
            && source.Length == SsMigrationFingerprints.SourcePrefix.Length + 64
            && source[SsMigrationFingerprints.SourcePrefix.Length..].All(char.IsAsciiHexDigit)
            && run.AuditSourceRowCount is not null;
        var sourceStatus =
            sourceAvailable ? "Available"
            : source == SsMigrationFingerprints.MultipleSnapshots ? "UnavailableMultipleSnapshots"
            : source is null ? "UnavailableNotCaptured"
            : "UnavailableUnknownFormat";
        var outcomes = await SsMigrationFingerprints.OutcomesAsync(db, runId, ct);
        var crosswalks = run.DryRun ? null : await SsMigrationFingerprints.CrosswalksAsync(db, runId, ct);
        var simulation = run.DryRun
            ? await db
                .DataMigrationSimulations.AsNoTracking()
                .Where(x => x.RunId == runId)
                .Select(x => new
                {
                    x.CandidateHash,
                    x.CandidateCount,
                    x.FormatVersion,
                })
                .SingleOrDefaultAsync(ct)
            : null;
        var failureKind = await db
            .DataMigrationFailures.AsNoTracking()
            .Where(x => x.RunId == runId && x.Source == "SS")
            .OrderByDescending(x => x.OccurredOn)
            .Select(x => x.ErrorType)
            .FirstOrDefaultAsync(ct);
        if (!MigrationTechnicalFailure.IsReportDiagnostic(failureKind))
            failureKind = null;

        var values = new
        {
            SummaryVersion = 1,
            RunId = run.Id,
            run.Source,
            Job = run.Job is "MyTeam" or "Training" or "Scheduling" or "Report" or "Legacy" ? run.Job : "Unknown",
            PersistenceMode = mode,
            run.DryRun,
            run.Status,
            CompletionCause = run.Status == "Abandoned" ? "StaleRunRecovery" : failureKind,
            Initiator = new { UserId = actor, Kind = kind },
            Counts = new
            {
                run.ScannedCount,
                run.InsertedCount,
                run.UpdatedCount,
                run.UnchangedCount,
                run.SkippedCount,
                run.RejectedCount,
                run.FailedCount,
                run.CommittedBatchCount,
            },
            Timestamps = new
            {
                run.StartedOn,
                run.CompletedOn,
                run.HeartbeatOn,
            },
            run.CutoverDate,
            run.HistoryStartDate,
            Fingerprints = new
            {
                SourceProjection = new
                {
                    Status = sourceStatus,
                    Format = SsMigrationFingerprints.SourceFormat,
                    Algorithm = "SHA-256",
                    Value = sourceAvailable ? source![SsMigrationFingerprints.SourcePrefix.Length..] : null,
                    Rows = sourceAvailable ? run.AuditSourceRowCount : null,
                },
                OutcomeEvidence = outcomes,
                AppliedCrosswalkEvidence = crosswalks,
                AppliedCrosswalkStatus = run.DryRun ? "NotApplicableDryRun" : "Available",
                SimulationCandidates = new
                {
                    Status = simulation is null ? "Unavailable" : "Available",
                    Algorithm = simulation is null ? null : "SHA-256",
                    FormatVersion = simulation?.FormatVersion,
                    Value = simulation?.CandidateHash,
                    Rows = simulation?.CandidateCount,
                },
                FullSource = "UnavailableNotCaptured",
                FullTarget = "UnavailableNotCaptured",
            },
        };
        db.AuditRecords.Add(
            new AuditRecord
            {
                OccurredOn = DateTimeOffset.UtcNow,
                ActorUserId = actor,
                ActorName = kind == "Scheduled" ? "System" : null,
                Action = "Added",
                EntityType = EntityType,
                EntityPK = key,
                TableName = "DataMigrationRuns",
                NewValues = JsonSerializer.Serialize(values),
            }
        );
        await db.SaveChangesAsync(ct);
    }

    private sealed record Trigger(Guid Actor, string Mode);

    private static async Task<Trigger?> FindTriggerAsync(
        UnifiedDbContext db,
        DataMigrationRun run,
        CancellationToken ct
    )
    {
        var key = run.Id.ToString("D");
        var records = await db
            .AuditRecords.AsNoTracking()
            .Where(x =>
                x.EntityType == "DataMigrationOperation"
                && x.TableName == "DataMigrationRuns"
                && x.Action == "Added"
                && x.EntityPK == key
                && x.ActorUserId != null
                && x.ActorUserId != Guid.Empty
            )
            .OrderBy(x => x.Id)
            .Select(x => new { x.ActorUserId, x.NewValues })
            .ToListAsync(ct);
        foreach (var record in records)
        {
            try
            {
                using var document = JsonDocument.Parse(record.NewValues ?? "");
                var root = document.RootElement;
                if (
                    root.GetProperty("Source").GetString() != run.Source
                    || root.GetProperty("Job").GetString() != run.Job
                    || root.GetProperty("RunId").GetGuid() != run.Id
                    || root.GetProperty("Operation").GetString() != "Trigger"
                    || root.GetProperty("DryRun").GetBoolean() != run.DryRun
                )
                    continue;
                // Before Bulk was introduced, a missing mode meant PerRow.
                var mode = root.TryGetProperty("PersistenceMode", out var value)
                    ? value.GetString() ?? "Unknown"
                    : "PerRow";
                return new(record.ActorUserId!.Value, mode);
            }
            catch (Exception ex)
                when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
            {
                // Invalid historical evidence is not evidence of an initiator.
            }
        }
        return null;
    }
}
