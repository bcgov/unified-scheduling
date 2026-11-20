using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

// Batch boundaries are BETWEEN indivisible writer units (including complete merge groups).
// Source snapshots remain complete to rehydrate groups, uniqueness checks and FK dependencies.
// Committed crosswalks are the replay fence; cursors and independent retry rows are durable.
internal sealed class SsBatchProgress(
    UnifiedDbContext db,
    SsWriteSession session,
    Dictionary<string, List<SsRow>> snapshot,
    DateOnly history,
    int batchSize,
    Func<CancellationToken, Task> fence
) : IAsyncDisposable
{
    private IDbContextTransaction? transaction;
    private int units;
    private readonly Dictionary<(string, string), string> flushed = [];
    private readonly Dictionary<string, int> positions = [];
    private Dictionary<(string, string), SsRow>? sourceIndex;
    private List<(string Table, List<(SsRow Row, string Type)> Rows)>? selectedTables;
    private readonly Dictionary<(string Type, string Id), (string Outcome, string? Reason)> counted = [];
    private readonly RunCounts counts = new();

    public static string Stream(DateOnly start) =>
        start < new DateOnly(2020, 1, 1) ? $"backfill:{start:yyyy-MM-dd}" : "normal";

    private string? OwnedType(string table, SsRow row) =>
        table switch
        {
            "LookupCode" => (session.Run.Job, row.Int("Type")) switch
            {
                ("MyTeam", 5) => "LeaveType",
                ("MyTeam", 7) => "PositionType",
                ("Training", 6) => "Training",
                ("Scheduling", 0) => "CourtRoom",
                ("Scheduling", >= 1 and <= 4) => "AssignmentClassification",
                _ => null,
            },
            "Region"
            or "Location"
            or "Role"
            or "User"
            or "UserRole"
            or "SheriffActingRank"
            or "SheriffAwayLocation"
            or "SheriffLeave"
            or "LookupSortOrder" when session.Run.Job == "MyTeam" => table,
            "SheriffTraining" or "SheriffStandardTraining" when session.Run.Job == "Training" => table,
            "Assignment" or "Duty" or "DutySlot" or "Shift" when session.Run.Job == "Scheduling" => table,
            _ => null,
        };

    public async Task BeginAsync(CancellationToken ct)
    {
        transaction = await db.Database.BeginTransactionAsync(ct);
        await fence(ct);
        session.TrackOutcomeChanges();
        session.AfterUnitAsync = async token =>
        {
            if (++units >= batchSize)
            {
                await CommitAsync(false, token);
                transaction = await db.Database.BeginTransactionAsync(token);
                await fence(token);
            }
        };
    }

    // The snapshot and job are fixed for the run, so the owned source rows are indexed once
    // (on the first commit) instead of on every batch.
    private void IndexSnapshot()
    {
        if (sourceIndex is not null)
            return;
        selectedTables = snapshot
            .Select(table =>
                (
                    Table: table.Key,
                    Rows: table
                        .Value.Select(row => (Row: row, Type: OwnedType(table.Key, row)))
                        .Where(x => x.Type is not null)
                        .Select(x => (x.Row, Type: x.Type!))
                        .ToList()
                )
            )
            .ToList();
        sourceIndex = selectedTables.SelectMany(table => table.Rows).ToDictionary(x => (x.Type, x.Row.Id), x => x.Row);
    }

    public async Task CommitAsync(bool final, CancellationToken ct)
    {
        await fence(ct);
        IndexSnapshot();
        var stream = Stream(history);
        // An outcome not touched since the previous commit still matches what that commit
        // flushed, so only touched keys can need an upsert. They are visited in list order,
        // exactly like a full scan; the final commit re-checks every outcome.
        var touched = session.TakeChangedOutcomeKeys();
        var candidates = final
            ? Enumerable.Range(0, session.Outcomes.Count).ToList()
            : touched.Select(x => session.OutcomePosition(x.Type, x.Id)).Where(x => x >= 0).Order().ToList();
        foreach (var outcome in candidates.Select(x => session.Outcomes[x]))
        {
            var key = (outcome.EntityType, outcome.LegacyId);
            var digest = $"{outcome.Outcome}:{outcome.Reason}";
            if (flushed.GetValueOrDefault(key) == digest)
                continue;
            await UpsertOutcomeAsync(db, outcome, ct);
            if (sourceIndex!.TryGetValue(key, out var source))
            {
                var pending = outcome.Outcome is "Rejected" or "Failed" or "NeedsAction" or "DroppedBadData";
                var defer =
                    outcome.Outcome == "Excluded"
                    && outcome.Reason?.StartsWith("Before", StringComparison.Ordinal) == true;
                var changed = source.Date("UpdatedOn") ?? source.Date("CreatedOn");
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO "DataMigrationWorkItems"
                    ("Source","Job","Stream","EntityType","LegacyId","Outcome","Reason","PendingRetry","Attempts","LastRunId","SourceChangedAtUtc")
                    VALUES ('SS',{session.Run.Job},{stream},{outcome.EntityType},{outcome.LegacyId},
                    {outcome.Outcome},{outcome.Reason},{pending},1,{session.Run.Id},{changed})
                    ON CONFLICT ("Source","Job","Stream","EntityType","LegacyId") DO UPDATE SET
                    "Outcome"=EXCLUDED."Outcome","Reason"=EXCLUDED."Reason",
                    "PendingRetry"=CASE WHEN {defer} THEN "DataMigrationWorkItems"."PendingRetry" ELSE EXCLUDED."PendingRetry" END,
                    "Attempts"="DataMigrationWorkItems"."Attempts"+CASE WHEN "DataMigrationWorkItems"."LastRunId"=EXCLUDED."LastRunId" THEN 0 ELSE 1 END,
                    "LastRunId"=EXCLUDED."LastRunId","SourceChangedAtUtc"=EXCLUDED."SourceChangedAtUtc"
                    """,
                    ct
                );
            }
            flushed[key] = digest;
        }
        foreach (var table in selectedTables!)
        {
            var selected = table.Rows;
            var position = positions.GetValueOrDefault(table.Table);
            var before = position;
            // Every owned type is a source (primary) type, so this is the source-outcome lookup.
            while (
                position < selected.Count
                && session.FindOutcome(selected[position].Type, selected[position].Row.Id) is not null
            )
                position++;
            if (position == before)
                continue;
            var row = selected[position - 1].Row;
            var upper = selected[^1].Row;
            var changed = row.Date("UpdatedOn") ?? row.Date("CreatedOn") ?? DateTimeOffset.MinValue;
            var upperChanged = upper.Date("UpdatedOn") ?? upper.Date("CreatedOn") ?? DateTimeOffset.MinValue;
            var idKind = Guid.TryParse(row.Id, out _) ? "Guid" : "Int32";
            // Progress never regresses, even when a later HistoryStartDate is requested.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "DataMigrationCheckpoints"
                ("Source","Job","Stream","EntityType","LegacyId","IdKind","SourceChangedAtUtc",
                "UpperLegacyId","UpperChangedAtUtc","LastRunId","CommittedBatches","HistoryStartDate")
                VALUES ('SS',{session.Run.Job},{stream},{table.Table},{row.Id},{idKind},{changed},
                {upper.Id},{upperChanged},{session.Run.Id},1,{history})
                ON CONFLICT ("Source","Job","Stream","EntityType") DO UPDATE SET
                "LegacyId"=CASE WHEN EXCLUDED."SourceChangedAtUtc">"DataMigrationCheckpoints"."SourceChangedAtUtc"
                    OR (EXCLUDED."SourceChangedAtUtc"="DataMigrationCheckpoints"."SourceChangedAtUtc"
                        AND EXCLUDED."IdKind"="DataMigrationCheckpoints"."IdKind"
                        AND CASE WHEN EXCLUDED."IdKind"='Guid'
                            THEN EXCLUDED."LegacyId"::uuid>"DataMigrationCheckpoints"."LegacyId"::uuid
                            ELSE EXCLUDED."LegacyId"::bigint>"DataMigrationCheckpoints"."LegacyId"::bigint END)
                    THEN EXCLUDED."LegacyId" ELSE "DataMigrationCheckpoints"."LegacyId" END,
                "SourceChangedAtUtc"=greatest("DataMigrationCheckpoints"."SourceChangedAtUtc",EXCLUDED."SourceChangedAtUtc"),
                "UpperLegacyId"=EXCLUDED."UpperLegacyId","UpperChangedAtUtc"=EXCLUDED."UpperChangedAtUtc",
                "LastRunId"=EXCLUDED."LastRunId","CommittedBatches"="DataMigrationCheckpoints"."CommittedBatches"+1,
                "HistoryStartDate"=least("DataMigrationCheckpoints"."HistoryStartDate",EXCLUDED."HistoryStartDate")
                """,
                ct
            );
            positions[table.Table] = position;
        }
        foreach (var key in touched)
            Recount(key);
        var run = await db.DataMigrationRuns.SingleAsync(x => x.Id == session.Run.Id, ct);
        run.HistoryStartDate = history;
        run.HeartbeatOn = DateTimeOffset.UtcNow;
        run.CommittedBatchCount++;
        if (!final)
            counts.ApplyTo(run, session);
        else
        {
            SetCounts(run, session);
            run.Status =
                run.FailedCount + run.RejectedCount > 0 ? "Failed"
                : session.Outcomes.Any(x => x.Outcome is "NeedsAction" or "DroppedBadData") ? "NeedsAction"
                : "Completed";
            run.CompletedOn = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        if (final)
            await SsMigrationRunAudit.WriteAsync(db, run.Id, ct);
        await transaction!.CommitAsync(ct);
        await transaction.DisposeAsync();
        transaction = null;
        units = 0;
        // Everything tracked is now saved and committed. Detach it so the tracker (which
        // every later SaveChanges and interceptor scans) stays batch-sized; the dry run
        // already detaches at these same unit boundaries.
        db.ChangeTracker.Clear();
    }

    // Running run counters for intermediate commits, maintained from the touched keys.
    // They always equal SetCounts over the current outcomes; the final commit uses SetCounts.
    private void Recount((string Type, string Id) key)
    {
        if (counted.Remove(key, out var prior))
            counts.Add(key.Type, prior.Outcome, prior.Reason, -1);
        if (session.FindOutcome(key.Type, key.Id) is { } current)
        {
            counts.Add(key.Type, current.Outcome, current.Reason, 1);
            counted[key] = (current.Outcome, current.Reason);
        }
    }

    internal sealed class RunCounts
    {
        public int Scanned { get; private set; }
        public int Unchanged { get; private set; }
        public int Skipped { get; private set; }
        public int Rejected { get; private set; }
        public int Failed { get; private set; }

        public void Add(string type, string outcome, string? reason, int delta)
        {
            var source = SsWriteSession.IsSourceType(type);
            if (source)
                Scanned += delta;
            if (reason is "ExistingCrosswalk" or "ExistingGroupCrosswalk" or "UnchangedMappedSource")
                Unchanged += delta;
            if (source && outcome == "Excluded")
                Skipped += delta;
            if (outcome == "Rejected")
                Rejected += delta;
            if (outcome == "Failed")
                Failed += delta;
        }

        public void ApplyTo(DataMigrationRun run, SsWriteSession session)
        {
            run.ScannedCount = Scanned;
            run.InsertedCount = session.Inserted;
            run.UpdatedCount = session.Updated;
            run.UnchangedCount = Unchanged;
            run.SkippedCount = Skipped;
            run.RejectedCount = Rejected;
            run.FailedCount = Failed;
        }
    }

    internal static void SetCounts(DataMigrationRun run, SsWriteSession session)
    {
        run.ScannedCount = session.SourceOutcomes.Count();
        run.InsertedCount = session.Inserted;
        run.UpdatedCount = session.Updated;
        run.UnchangedCount = session.Outcomes.Count(x =>
            x.Reason is "ExistingCrosswalk" or "ExistingGroupCrosswalk" or "UnchangedMappedSource"
        );
        run.SkippedCount = session.SourceOutcomes.Count(x => x.Outcome == "Excluded");
        run.RejectedCount = session.Outcomes.Count(x => x.Outcome == "Rejected");
        run.FailedCount = session.Outcomes.Count(x => x.Outcome == "Failed");
    }

    internal static Task<int> UpsertOutcomeAsync(UnifiedDbContext db, DataMigrationOutcome row, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "DataMigrationOutcomes" ("RunId","EntityType","LegacyId","Outcome","Reason")
            VALUES ({row.RunId},{row.EntityType},{row.LegacyId},{row.Outcome},{row.Reason})
            ON CONFLICT ("RunId","EntityType","LegacyId") DO UPDATE
            SET "Outcome"=EXCLUDED."Outcome","Reason"=EXCLUDED."Reason"
            """,
            ct
        );

    public async ValueTask DisposeAsync()
    {
        session.AfterUnitAsync = null;
        if (transaction is not null)
            await transaction.DisposeAsync();
    }
}
