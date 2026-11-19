using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services.Bulk;

/// <summary>
/// Run-scoped outcome/counter ledger for bulk jobs. Mirrors <see cref="SsBatchProgress"/> and
/// <see cref="SsWriteSession"/> accounting (outcomes, retry work items, checkpoints, run counters,
/// heartbeat and final status) with set-based writes, so the same run evidence is produced.
/// Pending changes are flushed inside the caller's chunk transaction and discarded on rollback.
/// </summary>
internal sealed class SsBulkLedger
{
    /// <summary>Must stay identical to <c>SsWriteSession.PrimaryTypes</c> (guarded by a unit test).</summary>
    internal static readonly IReadOnlySet<string> PrimaryTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "Region",
        "Location",
        "Role",
        "User",
        "UserRole",
        "PositionType",
        "LeaveType",
        "SheriffActingRank",
        "SheriffAwayLocation",
        "SheriffLeave",
        "LookupSortOrder",
        "Training",
        "SheriffTraining",
        "SheriffStandardTraining",
        "CourtRoom",
        "AssignmentClassification",
        "Assignment",
        "Duty",
        "DutySlot",
        "Shift",
        "LookupCode",
    };

    private sealed class Entry(string type, string id, string outcome, string? reason, int ordinal)
    {
        public string Type { get; } = type;
        public string Id { get; } = id;
        public string Outcome { get; set; } = outcome;
        public string? Reason { get; set; } = reason;
        public int Ordinal { get; } = ordinal;
        public string? Flushed { get; set; }
    }

    private sealed record Staged(
        int Ordinal,
        string Type,
        string Id,
        string Outcome,
        string? Reason,
        bool Work,
        bool Pending,
        DateTimeOffset? Changed
    );

    private static readonly SsBulkColumn<Staged>[] OutcomeColumns =
    [
        new("ordinal", "integer", NpgsqlDbType.Integer, x => x.Ordinal),
        new("entity_type", "text", NpgsqlDbType.Text, x => x.Type),
        new("legacy_id", "text", NpgsqlDbType.Text, x => x.Id),
        new("outcome", "text", NpgsqlDbType.Text, x => x.Outcome),
        new("reason", "text", NpgsqlDbType.Text, x => x.Reason),
        new("work", "boolean", NpgsqlDbType.Boolean, x => x.Work),
        new("pending", "boolean", NpgsqlDbType.Boolean, x => x.Pending),
        new("changed", "timestamptz", NpgsqlDbType.TimestampTz, x => x.Changed),
    ];

    private readonly UnifiedDbContext db;
    private readonly DataMigrationRun run;
    private readonly DateOnly history;
    private readonly Dictionary<(string, string), Entry> outcomes = [];
    private readonly List<Entry> order = [];
    private readonly HashSet<string> inserted = new(StringComparer.Ordinal);
    private readonly HashSet<string> updated = new(StringComparer.Ordinal);
    private readonly Dictionary<(string, string), SsRow> sourceIndex = [];
    private readonly List<(string Table, List<(string Type, SsRow Row)> Rows)> selected = [];
    private readonly Dictionary<string, int> positions = [];

    // Uncommitted chunk state (restored when a chunk transaction rolls back).
    private List<(Entry Entry, string Outcome, string? Reason)> changes = [];
    private int orderMark;
    private List<string> insertedMark = [];
    private List<string> updatedMark = [];

    internal SsBulkLedger(
        UnifiedDbContext db,
        DataMigrationRun run,
        Dictionary<string, List<SsRow>> snapshot,
        DateOnly history,
        Func<string, SsRow, string?> ownedType
    )
    {
        this.db = db;
        this.run = run;
        this.history = history;
        foreach (var table in snapshot)
        {
            var rows = table
                .Value.Select(row => (Type: ownedType(table.Key, row), Row: row))
                .Where(x => x.Type is not null)
                .Select(x => (x.Type!, x.Row))
                .ToList();
            foreach (var (type, row) in rows)
                sourceIndex[(type, row.Id)] = row;
            selected.Add((table.Key, rows));
        }
    }

    internal DataMigrationRun Run => run;
    internal int Inserted => inserted.Count;
    internal int Updated => updated.Count;

    internal void Outcome(string type, string id, string outcome, string? reason = null)
    {
        if (outcomes.TryGetValue((type, id), out var prior))
        {
            changes.Add((prior, prior.Outcome, prior.Reason));
            prior.Outcome = outcome;
            prior.Reason = reason;
            return;
        }
        var entry = new Entry(type, id, outcome, reason, order.Count);
        outcomes[(type, id)] = entry;
        order.Add(entry);
    }

    internal (string Outcome, string? Reason)? Find(string type, string id) =>
        outcomes.TryGetValue((type, id), out var entry) ? (entry.Outcome, entry.Reason) : null;

    internal void Insert(string targetKey)
    {
        if (inserted.Add(targetKey))
            insertedMark.Add(targetKey);
    }

    internal void Update(string targetKey)
    {
        if (updated.Add(targetKey))
            updatedMark.Add(targetKey);
    }

    /// <summary>Discards everything recorded since the last successful flush (chunk rollback).</summary>
    internal void Discard()
    {
        for (var i = changes.Count - 1; i >= 0; i--)
        {
            changes[i].Entry.Outcome = changes[i].Outcome;
            changes[i].Entry.Reason = changes[i].Reason;
        }
        foreach (var entry in order.Skip(orderMark))
            outcomes.Remove((entry.Type, entry.Id));
        order.RemoveRange(orderMark, order.Count - orderMark);
        inserted.ExceptWith(insertedMark);
        updated.ExceptWith(updatedMark);
        Accept(false);
    }

    /// <summary>
    /// Writes changed outcomes, retry work items, checkpoint progress, counters, heartbeat and
    /// (when <paramref name="final"/>) the terminal status, inside the open chunk transaction.
    /// </summary>
    internal async Task FlushAsync(bool final, CancellationToken ct)
    {
        var connection = SsBulkStaging.Connection(db);
        var stream = SsBatchProgress.Stream(history);
        var staged = order
            .Where(x => x.Flushed != $"{x.Outcome}:{x.Reason}")
            .Select(x =>
            {
                var source = sourceIndex.GetValueOrDefault((x.Type, x.Id));
                return new Staged(
                    x.Ordinal,
                    x.Type,
                    x.Id,
                    x.Outcome,
                    x.Reason,
                    source is not null,
                    x.Outcome is "Rejected" or "Failed" or "NeedsAction" or "DroppedBadData",
                    source?.Date("UpdatedOn") ?? source?.Date("CreatedOn")
                );
            })
            .ToList();
        if (staged.Count > 0)
        {
            await SsBulkStaging.StageAsync(connection, "ss_bulk_outcomes", OutcomeColumns, staged, ct);
            await SsBulkStaging.ExecuteAsync(
                connection,
                """
                INSERT INTO "DataMigrationOutcomes" ("RunId","EntityType","LegacyId","Outcome","Reason")
                SELECT @run, entity_type, legacy_id, outcome, reason FROM ss_bulk_outcomes ORDER BY ordinal
                ON CONFLICT ("RunId","EntityType","LegacyId") DO UPDATE
                SET "Outcome"=EXCLUDED."Outcome","Reason"=EXCLUDED."Reason"
                """,
                ct,
                new NpgsqlParameter("run", run.Id)
            );
            // Same upsert as SsBatchProgress; "defer" derives from the outcome/reason pair.
            await SsBulkStaging.ExecuteAsync(
                connection,
                """
                INSERT INTO "DataMigrationWorkItems"
                ("Source","Job","Stream","EntityType","LegacyId","Outcome","Reason","PendingRetry","Attempts","LastRunId","SourceChangedAtUtc")
                SELECT 'SS', @job, @stream, entity_type, legacy_id, outcome, reason, pending, 1, @run, changed
                FROM ss_bulk_outcomes WHERE work ORDER BY ordinal
                ON CONFLICT ("Source","Job","Stream","EntityType","LegacyId") DO UPDATE SET
                "Outcome"=EXCLUDED."Outcome","Reason"=EXCLUDED."Reason",
                "PendingRetry"=CASE WHEN EXCLUDED."Outcome"='Excluded' AND EXCLUDED."Reason" LIKE 'Before%'
                    THEN "DataMigrationWorkItems"."PendingRetry" ELSE EXCLUDED."PendingRetry" END,
                "Attempts"="DataMigrationWorkItems"."Attempts"+CASE WHEN "DataMigrationWorkItems"."LastRunId"=EXCLUDED."LastRunId" THEN 0 ELSE 1 END,
                "LastRunId"=EXCLUDED."LastRunId","SourceChangedAtUtc"=EXCLUDED."SourceChangedAtUtc"
                """,
                ct,
                new NpgsqlParameter("run", run.Id),
                new NpgsqlParameter("job", run.Job),
                new NpgsqlParameter("stream", stream)
            );
        }
        var newPositions = new Dictionary<string, int>();
        foreach (var (table, rows) in selected)
        {
            var position = positions.GetValueOrDefault(table);
            var before = position;
            while (position < rows.Count && outcomes.ContainsKey((rows[position].Type, rows[position].Row.Id)))
                position++;
            if (position == before)
                continue;
            var row = rows[position - 1].Row;
            var upper = rows[^1].Row;
            var changed = row.Date("UpdatedOn") ?? row.Date("CreatedOn") ?? DateTimeOffset.MinValue;
            var upperChanged = upper.Date("UpdatedOn") ?? upper.Date("CreatedOn") ?? DateTimeOffset.MinValue;
            var idKind = Guid.TryParse(row.Id, out _) ? "Guid" : "Int32";
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "DataMigrationCheckpoints"
                ("Source","Job","Stream","EntityType","LegacyId","IdKind","SourceChangedAtUtc",
                "UpperLegacyId","UpperChangedAtUtc","LastRunId","CommittedBatches","HistoryStartDate")
                VALUES ('SS',{run.Job},{stream},{table},{row.Id},{idKind},{changed},
                {upper.Id},{upperChanged},{run.Id},1,{history})
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
            newPositions[table] = position;
        }
        var persisted = await db.DataMigrationRuns.SingleAsync(x => x.Id == run.Id, ct);
        persisted.HistoryStartDate = history;
        persisted.HeartbeatOn = DateTimeOffset.UtcNow;
        persisted.CommittedBatchCount++;
        var primary = order.Where(x => PrimaryTypes.Contains(x.Type)).ToList();
        persisted.ScannedCount = primary.Count;
        persisted.InsertedCount = inserted.Count;
        persisted.UpdatedCount = updated.Count;
        persisted.UnchangedCount = order.Count(x =>
            x.Reason is "ExistingCrosswalk" or "ExistingGroupCrosswalk" or "UnchangedMappedSource"
        );
        persisted.SkippedCount = primary.Count(x => x.Outcome == "Excluded");
        persisted.RejectedCount = order.Count(x => x.Outcome == "Rejected");
        persisted.FailedCount = order.Count(x => x.Outcome == "Failed");
        if (final)
        {
            persisted.Status =
                persisted.FailedCount + persisted.RejectedCount > 0 ? "Failed"
                : order.Any(x => x.Outcome is "NeedsAction" or "DroppedBadData") ? "NeedsAction"
                : "Completed";
            persisted.CompletedOn = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        pendingPositions = newPositions;
    }

    private Dictionary<string, int> pendingPositions = [];

    /// <summary>Marks the flushed state durable after the chunk transaction committed.</summary>
    internal void Accept(bool committed = true)
    {
        if (committed)
        {
            foreach (var entry in order)
                entry.Flushed = $"{entry.Outcome}:{entry.Reason}";
            foreach (var position in pendingPositions)
                positions[position.Key] = position.Value;
        }
        pendingPositions = [];
        changes = [];
        orderMark = order.Count;
        insertedMark = [];
        updatedMark = [];
    }
}
