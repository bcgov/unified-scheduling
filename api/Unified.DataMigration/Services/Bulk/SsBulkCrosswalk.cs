using Npgsql;
using NpgsqlTypes;

namespace Unified.DataMigration.Services.Bulk;

/// <summary>A new crosswalk row (source key to target key) written by a bulk chunk.</summary>
internal sealed record SsBulkLink(
    string EntityType,
    string LegacyId,
    string TargetType,
    string TargetKey,
    string PayloadHash,
    string AfterHash,
    Guid? CreatedByRunId,
    DateTimeOffset? SourceUpdatedAtUtc
);

/// <summary>A replacement of mutable receipt fields on an existing crosswalk row.</summary>
internal sealed record SsBulkRecordUpdate(
    long RecordId,
    string PayloadHash,
    string? AfterHash,
    DateTimeOffset? SourceUpdatedAtUtc,
    bool SetLastApplied
);

/// <summary>
/// One receipt mutation. Exactly one of <see cref="RecordId"/> or (<see cref="EntityType"/>,
/// <see cref="LegacyId"/>) identifies the record; new links are resolved by explicit key join.
/// </summary>
internal sealed record SsBulkMutation(
    long? RecordId,
    string? EntityType,
    string? LegacyId,
    string Operation,
    string AfterHash,
    string? Version
);

/// <summary>
/// Set-based crosswalk/receipt writes shared by bulk jobs. Idempotency: the unique
/// (Source, EntityType, LegacyId) index plus <c>ON CONFLICT DO NOTHING</c>; a conflict means another
/// writer owns the key, so the chunk is aborted (rolled back) rather than creating duplicates.
/// Mutation sequences continue the run's per-run sequence in the caller's processing order.
/// </summary>
internal static class SsBulkCrosswalk
{
    private static readonly SsBulkColumn<(int Ordinal, SsBulkLink Link)>[] LinkColumns =
    [
        new("ordinal", "integer", NpgsqlDbType.Integer, x => x.Ordinal),
        new("entity_type", "text", NpgsqlDbType.Text, x => x.Link.EntityType),
        new("legacy_id", "text", NpgsqlDbType.Text, x => x.Link.LegacyId),
        new("target_type", "text", NpgsqlDbType.Text, x => x.Link.TargetType),
        new("target_key", "text", NpgsqlDbType.Text, x => x.Link.TargetKey),
        new("payload_hash", "text", NpgsqlDbType.Text, x => x.Link.PayloadHash),
        new("after_hash", "text", NpgsqlDbType.Text, x => x.Link.AfterHash),
        new("created_by", "uuid", NpgsqlDbType.Uuid, x => x.Link.CreatedByRunId),
        new("source_updated", "timestamptz", NpgsqlDbType.TimestampTz, x => x.Link.SourceUpdatedAtUtc),
    ];

    private static readonly SsBulkColumn<SsBulkRecordUpdate>[] UpdateColumns =
    [
        new("record_id", "bigint", NpgsqlDbType.Bigint, x => x.RecordId),
        new("payload_hash", "text", NpgsqlDbType.Text, x => x.PayloadHash),
        new("after_hash", "text", NpgsqlDbType.Text, x => x.AfterHash),
        new("source_updated", "timestamptz", NpgsqlDbType.TimestampTz, x => x.SourceUpdatedAtUtc),
        new("set_last_applied", "boolean", NpgsqlDbType.Boolean, x => x.SetLastApplied),
    ];

    private static readonly SsBulkColumn<(int Ordinal, SsBulkMutation Mutation)>[] MutationColumns =
    [
        new("ordinal", "integer", NpgsqlDbType.Integer, x => x.Ordinal),
        new("record_id", "bigint", NpgsqlDbType.Bigint, x => x.Mutation.RecordId),
        new("entity_type", "text", NpgsqlDbType.Text, x => x.Mutation.EntityType),
        new("legacy_id", "text", NpgsqlDbType.Text, x => x.Mutation.LegacyId),
        new("operation", "text", NpgsqlDbType.Text, x => x.Mutation.Operation),
        new("after_hash", "text", NpgsqlDbType.Text, x => x.Mutation.AfterHash),
        new("version", "text", NpgsqlDbType.Text, x => x.Mutation.Version),
    ];

    /// <summary>Inserts new crosswalk rows. Throws when any key already exists (no duplicates are created).</summary>
    internal static async Task InsertLinksAsync(
        NpgsqlConnection connection,
        Guid runId,
        IReadOnlyList<SsBulkLink> links,
        CancellationToken ct
    )
    {
        if (links.Count == 0)
            return;
        await SsBulkStaging.StageAsync(
            connection,
            "ss_bulk_links",
            LinkColumns,
            links.Select((link, index) => (index, link)),
            ct
        );
        var inserted = await SsBulkStaging.ExecuteAsync(
            connection,
            """
            INSERT INTO "DataMigrationRecords"
            ("Source","EntityType","LegacyId","TargetType","TargetKey","PayloadHash","AfterHash",
             "CreatedByRunId","SourceUpdatedAtUtc","LastAppliedRunId","IsDeleted")
            SELECT 'SS', s.entity_type, s.legacy_id, s.target_type, s.target_key, s.payload_hash, s.after_hash,
                   s.created_by, s.source_updated, @run, false
            FROM ss_bulk_links s
            WHERE NOT EXISTS (
                SELECT 1 FROM "DataMigrationRecords" r
                WHERE r."Source"='SS' AND r."EntityType"=s.entity_type AND r."LegacyId"=s.legacy_id)
            ORDER BY s.ordinal
            ON CONFLICT ("Source","EntityType","LegacyId") DO NOTHING
            """,
            ct,
            new NpgsqlParameter("run", runId)
        );
        if (inserted != links.Count)
            throw new InvalidOperationException("SS bulk crosswalk key already exists; chunk rolled back.");
    }

    internal static async Task UpdateRecordsAsync(
        NpgsqlConnection connection,
        Guid runId,
        IReadOnlyList<SsBulkRecordUpdate> updates,
        CancellationToken ct
    )
    {
        if (updates.Count == 0)
            return;
        if (updates.Select(x => x.RecordId).Distinct().Count() != updates.Count)
            throw new InvalidOperationException("SS bulk record update batch must have one row per record.");
        await SsBulkStaging.StageAsync(connection, "ss_bulk_record_updates", UpdateColumns, updates, ct);
        var updated = await SsBulkStaging.ExecuteAsync(
            connection,
            """
            UPDATE "DataMigrationRecords" r SET
              "PayloadHash"=s.payload_hash,
              "AfterHash"=s.after_hash,
              "SourceUpdatedAtUtc"=s.source_updated,
              "LastAppliedRunId"=CASE WHEN s.set_last_applied THEN @run ELSE r."LastAppliedRunId" END
            FROM ss_bulk_record_updates s
            WHERE r."Id"=s.record_id
            """,
            ct,
            new NpgsqlParameter("run", runId)
        );
        if (updated != updates.Count)
            throw new InvalidOperationException("SS bulk crosswalk record disappeared; chunk rolled back.");
    }

    /// <summary>Appends receipts with <c>Sequence</c> continuing the run's maximum, in list order.</summary>
    internal static async Task AppendMutationsAsync(
        NpgsqlConnection connection,
        Guid runId,
        IReadOnlyList<SsBulkMutation> mutations,
        CancellationToken ct
    )
    {
        if (mutations.Count == 0)
            return;
        await SsBulkStaging.StageAsync(
            connection,
            "ss_bulk_mutations",
            MutationColumns,
            mutations.Select((mutation, index) => (index, mutation)),
            ct
        );
        var inserted = await SsBulkStaging.ExecuteAsync(
            connection,
            """
            INSERT INTO "DataMigrationMutations"
            ("RunId","DataMigrationRecordId","Sequence","Operation","AfterHash","Version")
            SELECT @run, COALESCE(s.record_id, r."Id"),
                   base.value + row_number() OVER (ORDER BY s.ordinal),
                   s.operation, s.after_hash, s.version
            FROM ss_bulk_mutations s
            LEFT JOIN "DataMigrationRecords" r
              ON s.record_id IS NULL AND r."Source"='SS' AND r."EntityType"=s.entity_type
             AND r."LegacyId"=s.legacy_id
            CROSS JOIN (SELECT COALESCE(max("Sequence"),0) AS value
                        FROM "DataMigrationMutations" WHERE "RunId"=@run) base
            WHERE COALESCE(s.record_id, r."Id") IS NOT NULL
            ORDER BY s.ordinal
            """,
            ct,
            new NpgsqlParameter("run", runId)
        );
        if (inserted != mutations.Count)
            throw new InvalidOperationException("SS bulk receipt could not resolve its crosswalk record.");
    }
}
