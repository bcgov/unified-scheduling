using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Unified.Db;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.UserManagement;

namespace Unified.DataMigration.Services.Bulk;

/// <summary>
/// Optional business-table audit rows for bulk writes. Bulk SQL bypasses Audit.NET, so when
/// auditing is enabled these <c>AuditRecords</c> are written by binary COPY. SS migration
/// execution disables auditing only on its dedicated context; consequently its bulk writes
/// produce no per-row audit records and are represented by the explicit terminal run summary.
/// Migration ledger tables (DataMigration*) are not audited here.
/// </summary>
internal sealed class SsBulkAudit(UnifiedDbContext db)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Columns <c>AuditableEntityInterceptor</c> stamps on modified rows. Audit.NET snapshots the change set
    /// before EF runs save interceptors, so a stamped <c>UpdatedById</c> never appears in PerRow Modified
    /// records (<c>UpdatedOn</c> is added back by <c>AuditRecordEntityAction</c> and stays included).
    /// </summary>
    private static readonly HashSet<string> InterceptorStampedColumns = new(StringComparer.Ordinal)
    {
        nameof(BaseEntity.UpdatedById),
    };

    private sealed record Row(
        string Action,
        string EntityType,
        string Key,
        string Table,
        string? Old,
        string? New,
        string[]? Columns
    );

    private readonly List<Row> rows = [];
    private readonly bool enabled = SsBulkTargets.AuditEnabled(db);

    internal void Added(string entityType, string table, IEnumerable<SsBulkTargetImage> images)
    {
        if (!enabled)
            return;
        foreach (var image in images)
            rows.Add(
                new(
                    "Added",
                    entityType,
                    image.Key,
                    table,
                    null,
                    image.AuditValues.Count == 0
                        ? null
                        : JsonSerializer.Serialize(image.AuditValues, SerializerOptions),
                    null
                )
            );
    }

    internal void Modified(string entityType, string table, SsBulkTargetImage before, SsBulkTargetImage after)
    {
        if (!enabled)
            return;
        // No persisted change at all: EF would not have saved, so Audit.NET writes nothing.
        if (
            before.PersistedValues.Count == after.PersistedValues.Count
            && after.PersistedValues.All(x => ValueEquals(before.PersistedValues.GetValueOrDefault(x.Key), x.Value))
        )
            return;
        var columns = after
            .AuditValues.Where(x =>
                !InterceptorStampedColumns.Contains(x.Key)
                && !ValueEquals(before.AuditValues.GetValueOrDefault(x.Key), x.Value)
            )
            .Select(x => x.Key)
            .ToList();
        // Every changed column excluded: keep the operation (actor, key) with no values, like Audit.NET.
        if (columns.Count == 0)
        {
            rows.Add(new("Modified", entityType, after.Key, table, null, null, null));
            return;
        }
        rows.Add(
            new(
                "Modified",
                entityType,
                after.Key,
                table,
                JsonSerializer.Serialize(
                    columns.ToDictionary(x => x, x => before.AuditValues.GetValueOrDefault(x)),
                    SerializerOptions
                ),
                JsonSerializer.Serialize(columns.ToDictionary(x => x, x => after.AuditValues[x]), SerializerOptions),
                [.. columns]
            )
        );
    }

    internal async Task FlushAsync(CancellationToken ct)
    {
        if (rows.Count == 0)
            return;
        var connection = SsBulkStaging.Connection(db);
        var now = DateTimeOffset.UtcNow;
        var correlation = Activity.Current?.TraceId.ToString();
        await using (
            var importer = await SsBulkStaging.ImportAsync(
                connection,
                """
                COPY "AuditRecords" ("OccurredOn","ActorUserId","ActorName","Action","EntityType","EntityPK",
                "TableName","OldValues","NewValues","ChangedColumns","CorrelationId") FROM STDIN (FORMAT BINARY)
                """,
                ct
            )
        )
        {
            foreach (var row in rows)
            {
                await importer.StartRowAsync(ct);
                await importer.WriteAsync(now, NpgsqlDbType.TimestampTz, ct);
                await importer.WriteAsync(User.SystemUser, NpgsqlDbType.Uuid, ct);
                await importer.WriteAsync("System", NpgsqlDbType.Text, ct);
                await importer.WriteAsync(row.Action, NpgsqlDbType.Text, ct);
                await importer.WriteAsync(row.EntityType, NpgsqlDbType.Text, ct);
                await importer.WriteAsync(row.Key, NpgsqlDbType.Text, ct);
                await importer.WriteAsync(row.Table, NpgsqlDbType.Text, ct);
                await WriteNullableAsync(importer, row.Old, NpgsqlDbType.Jsonb, ct);
                await WriteNullableAsync(importer, row.New, NpgsqlDbType.Jsonb, ct);
                if (row.Columns is null)
                    await importer.WriteNullAsync(ct);
                else
                    await importer.WriteAsync(row.Columns, NpgsqlDbType.Array | NpgsqlDbType.Text, ct);
                await WriteNullableAsync(importer, correlation, NpgsqlDbType.Text, ct);
            }
            await importer.CompleteAsync(ct);
        }
        rows.Clear();
    }

    // Arrays compare by content, like EF's array value comparers (an equal re-assigned array is not a change).
    private static bool ValueEquals(object? left, object? right) =>
        left switch
        {
            byte[] a when right is byte[] b => a.AsSpan().SequenceEqual(b),
            string[] a when right is string[] b => a.SequenceEqual(b, StringComparer.Ordinal),
            _ => Equals(left, right),
        };

    private static Task WriteNullableAsync(
        NpgsqlBinaryImporter importer,
        string? value,
        NpgsqlDbType type,
        CancellationToken ct
    ) => value is null ? importer.WriteNullAsync(ct) : importer.WriteAsync(value, type, ct);
}
