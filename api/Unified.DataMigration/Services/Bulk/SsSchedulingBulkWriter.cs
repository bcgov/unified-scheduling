using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Unified.Audit.Options;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services.Bulk;

/// <summary>
/// Opt-in Bulk persistence for the SS Scheduling job (<c>PersistenceMode=Bulk</c>). The SAME Scheduling
/// rules as PerRow (<see cref="SsSchedulingWriter"/>, graph replay, family expansion, series writer and
/// <see cref="SsWriteSession"/>) run unchanged against <see cref="SsSchedulingSim"/>, an in-memory image
/// of the target loaded in one snapshot. Accepted rows are persisted per chunk (whole writer units, so a
/// merge group or graph is never split) in ONE transaction with binary COPY and staged set-based
/// updates; generated IDs are pre-allocated from the table sequences, never mapped by RETURNING order.
/// <para>
/// A complete no-write preflight pass runs first: any path Bulk cannot reproduce exactly
/// (<see cref="SsBulkRefusal"/>) fails the run before the first business write with
/// <c>BulkRequiresPerRow:&lt;reason&gt;</c>. No rollback journal is captured; see <see cref="SsBulkUndo"/>.
/// </para>
/// </summary>
public sealed class SsSchedulingBulkWriter(UnifiedDbContext db, IOptions<AuditRecordOptions>? auditOptions = null)
{
    /// <summary>Test hook invoked inside each chunk transaction just before commit (chunk index).</summary>
    internal Func<int, CancellationToken, Task>? BeforeChunkCommitAsync { get; set; }

    /// <summary>Test hook invoked after the preflight pass, before the real pass loads its snapshot.</summary>
    internal Func<CancellationToken, Task>? AfterPreflightAsync { get; set; }

    /// <summary>Same owned-row selection as <see cref="SsBatchProgress"/> for the Scheduling job.</summary>
    internal static string? OwnedType(string table, SsRow row) =>
        table switch
        {
            "LookupCode" => row.Int("Type") switch
            {
                0 => "CourtRoom",
                >= 1 and <= 4 => "AssignmentClassification",
                _ => null,
            },
            "Assignment" or "Duty" or "DutySlot" or "Shift" => table,
            _ => null,
        };

    internal async Task ExecuteAsync(
        DataMigrationRun run,
        Dictionary<string, List<SsRow>> rows,
        DateTimeOffset boundary,
        DateOnly history,
        int batchSize,
        int chunkSize,
        bool validatorsRegistered,
        Func<CancellationToken, Task> fence,
        CancellationToken ct
    )
    {
        if (run.Job != "Scheduling" || run.DryRun || run.StartedOn is null)
            throw new InvalidOperationException("SS Scheduling bulk persistence requires an applied Scheduling run.");
        if (chunkSize <= 0 || batchSize <= 0)
            throw new InvalidOperationException("SS Scheduling bulk batch and chunk sizes must be positive.");
        var audit = auditOptions?.Value ?? new AuditRecordOptions();
        var ledger = new SsBulkLedger(db, run, rows, history, OwnedType);
        var runner = new SsBulkChunkRunner(db, ledger, fence, BeforeChunkCommitAsync);
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            // Business validators read the target through EF inside the PerRow transaction; their
            // findings cannot be reproduced against the uncommitted in-memory image.
            if (validatorsRegistered)
                throw new SsBulkRefusal("SchedulingBusinessValidatorsRegistered");
            // A Training run writes Users and crosswalks while Bulk fences those tables; run them one after the
            // other (a Training run started later stops this run at its next chunk with TargetChangedDuringBulkRun).
            var trainingRunning = await SsBulkStaging.QueryAsync(
                SsBulkStaging.Connection(db),
                "SELECT count(*)::int FROM \"DataMigrationRuns\" WHERE \"Source\"='SS' AND \"Job\"='Training' AND \"Status\"='Running'",
                reader => reader.GetInt32(0),
                ct
            );
            if (trainingRunning.Single() > 0)
                throw new SsBulkRefusal("TrainingRunInProgress");
            string validated;
            using (var preflight = await SsSchedulingSim.LoadAsync(db, run, audit, ct))
            {
                await PassAsync(preflight, run, rows, boundary, batchSize, null, ct);
                validated = preflight.Digest;
            }
            if (AfterPreflightAsync is not null)
                await AfterPreflightAsync(ct);
            using var sim = await SsSchedulingSim.LoadAsync(db, run, audit, ct);
            // The real pass must replay exactly the target state the preflight validated; otherwise a refusal
            // could surface only after earlier chunks committed.
            if (sim.Digest != validated)
                throw new SsBulkRefusal("TargetChangedDuringBulkRun");
            await PassAsync(sim, run, rows, boundary, batchSize, new Chunks(db, sim, ledger, runner, chunkSize), ct);
        }
        catch (SsBulkRefusal refusal)
        {
            // Nothing of the refused chunk was committed; earlier committed chunks are complete,
            // crosswalked units that a PerRow run resumes like its own committed batches.
            ct.ThrowIfCancellationRequested();
            await runner.RunAsync(
                (_, _) =>
                {
                    ledger.Outcome(
                        "SchedulingBulk",
                        run.Id.ToString(),
                        "Failed",
                        "BulkRequiresPerRow:" + refusal.Message
                    );
                    return Task.CompletedTask;
                },
                ct,
                final: true
            );
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task PassAsync(
        SsSchedulingSim sim,
        DataMigrationRun run,
        Dictionary<string, List<SsRow>> rows,
        DateTimeOffset boundary,
        int batchSize,
        Chunks? chunks,
        CancellationToken ct
    )
    {
        var session = new SsWriteSession(db, run, sim);
        session.TrackOutcomeChanges();
        var units = 0;
        // Batch boundaries mirror SsBatchProgress exactly (save + tracker clear between units), so
        // tracker-dependent rule behaviour is identical; chunk boundaries are a multiple of units.
        session.AfterUnitAsync = async token =>
        {
            if (++units >= batchSize)
            {
                await sim.SaveAsync(token);
                sim.ClearTracker();
                units = 0;
            }
            if (chunks is not null)
                await chunks.AfterUnitAsync(session, false, token);
        };
        await new SsSchedulingWriter(db, sim).WriteCoreAsync(rows, session, boundary, ct);
        session.AfterUnitAsync = null;
        // Same evidence as PerRow without registered validators (validators refuse Bulk above).
        session.Outcome(
            "SchedulingValidation",
            run.Id.ToString(),
            "NeedsAction",
            "ActualSchedulingReadValidatorUnavailable"
        );
        await sim.SaveAsync(ct);
        if (chunks is not null)
            await chunks.AfterUnitAsync(session, true, ct);
    }

    private sealed class Chunks(
        UnifiedDbContext db,
        SsSchedulingSim sim,
        SsBulkLedger ledger,
        SsBulkChunkRunner runner,
        int chunkSize
    )
    {
        private int units;
        private int insertedFed;
        private int updatedFed;

        public async Task AfterUnitAsync(SsWriteSession session, bool final, CancellationToken ct)
        {
            if (!final && ++units < chunkSize)
                return;
            units = 0;
            await sim.SaveAsync(ct);
            uint? xid = null;
            await runner.RunAsync(
                async (connection, token) =>
                {
                    Feed(session);
                    xid = await sim.FlushAsync(connection, new SsBulkAudit(db), token);
                },
                ct,
                final
            );
            sim.Committed(xid!.Value);
        }

        private void Feed(SsWriteSession session)
        {
            foreach (
                var (type, id, position) in session
                    .TakeChangedOutcomeKeys()
                    .Select(x => (x.Type, x.Id, Position: session.OutcomePosition(x.Type, x.Id)))
                    .Where(x => x.Position >= 0)
                    .OrderBy(x => x.Position)
            )
            {
                var outcome = session.Outcomes[position];
                ledger.Outcome(type, id, outcome.Outcome, outcome.Reason);
            }
            foreach (var key in session.InsertedKeys.Skip(insertedFed))
                ledger.Insert(key);
            foreach (var key in session.UpdatedKeys.Skip(updatedFed))
                ledger.Update(key);
            insertedFed = session.InsertedKeys.Count;
            updatedFed = session.UpdatedKeys.Count;
        }
    }
}
