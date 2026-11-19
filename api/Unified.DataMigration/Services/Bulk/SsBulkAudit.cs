using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Unified.Db;
using Unified.Db.Models.UserManagement;

namespace Unified.DataMigration.Services.Bulk;

/// <summary>
/// Business-table audit rows for bulk writes. PerRow saves are audited by Audit.NET through
/// <c>SaveChanges</c>; bulk SQL bypasses it, so equivalent <c>AuditRecords</c> (Added / Modified,
/// background "System" actor, same column exclusions and JSON shape) are written by binary COPY.
/// Migration ledger tables (DataMigration*) are not audited in Bulk mode.
/// No-op when auditing is disabled (tests and contexts with <c>AuditDisabled</c>).
/// </summary>
internal sealed class SsBulkAudit(UnifiedDbContext db)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

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
        var columns = after
            .AuditValues.Where(x => !Equals(before.AuditValues.GetValueOrDefault(x.Key), x.Value))
            .Select(x => x.Key)
            .ToList();
        if (columns.Count == 0)
            return;
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
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var now = DateTimeOffset.UtcNow;
        var correlation = Activity.Current?.TraceId.ToString();
        await using (
            var importer = await connection.BeginBinaryImportAsync(
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

    private static Task WriteNullableAsync(
        NpgsqlBinaryImporter importer,
        string? value,
        NpgsqlDbType type,
        CancellationToken ct
    ) => value is null ? importer.WriteNullAsync(ct) : importer.WriteAsync(value, type, ct);
}
