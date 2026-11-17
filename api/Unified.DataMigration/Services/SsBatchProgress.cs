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

    public async Task CommitAsync(bool final, CancellationToken ct)
    {
        await fence(ct);
        var sourceOutcomes = session.SourceOutcomes.ToDictionary(x => (x.EntityType, x.LegacyId));
        var sourceIndex = snapshot
            .SelectMany(table => table.Value.Select(row => (Type: OwnedType(table.Key, row), Row: row)))
            .Where(x => x.Type is not null)
            .ToDictionary(x => (x.Type!, x.Row.Id), x => x.Row);
        var stream = Stream(history);
        foreach (var outcome in session.Outcomes)
        {
            var key = (outcome.EntityType, outcome.LegacyId);
            var digest = $"{outcome.Outcome}:{outcome.Reason}";
            if (flushed.GetValueOrDefault(key) == digest)
                continue;
            await UpsertOutcomeAsync(db, outcome, ct);
            if (sourceIndex.TryGetValue(key, out var source))
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
        foreach (var table in snapshot)
        {
            var selected = table.Value.Where(x => OwnedType(table.Key, x) is not null).ToList();
            var position = positions.GetValueOrDefault(table.Key);
            var before = position;
            while (
                position < selected.Count
                && sourceOutcomes.ContainsKey((OwnedType(table.Key, selected[position])!, selected[position].Id))
            )
                position++;
            if (position == before)
                continue;
            var row = selected[position - 1];
            var upper = selected[^1];
            var changed = row.Date("UpdatedOn") ?? row.Date("CreatedOn") ?? DateTimeOffset.MinValue;
            var upperChanged = upper.Date("UpdatedOn") ?? upper.Date("CreatedOn") ?? DateTimeOffset.MinValue;
            var idKind = Guid.TryParse(row.Id, out _) ? "Guid" : "Int32";
            // Progress never regresses, even when a later HistoryStartDate is requested.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "DataMigrationCheckpoints"
                ("Source","Job","Stream","EntityType","LegacyId","IdKind","SourceChangedAtUtc",
                "UpperLegacyId","UpperChangedAtUtc","LastRunId","CommittedBatches","HistoryStartDate")
                VALUES ('SS',{session.Run.Job},{stream},{table.Key},{row.Id},{idKind},{changed},
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
            positions[table.Key] = position;
        }
        var run = await db.DataMigrationRuns.SingleAsync(x => x.Id == session.Run.Id, ct);
        run.HistoryStartDate = history;
        run.HeartbeatOn = DateTimeOffset.UtcNow;
        run.CommittedBatchCount++;
        SetCounts(run, session);
        if (final)
        {
            run.Status =
                run.FailedCount + run.RejectedCount > 0 ? "Failed"
                : session.Outcomes.Any(x => x.Outcome is "NeedsAction" or "DroppedBadData") ? "NeedsAction"
                : "Completed";
            run.CompletedOn = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        await transaction!.CommitAsync(ct);
        await transaction.DisposeAsync();
        transaction = null;
        units = 0;
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
