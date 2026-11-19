using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;

namespace Unified.DataMigration.Services.Bulk;

/// <summary>One column of a temp staging table: name, PostgreSQL type and a value projection.</summary>
internal sealed record SsBulkColumn<T>(string Name, string SqlType, NpgsqlDbType DbType, Func<T, object?> Value);

/// <summary>
/// Generic transaction-scoped staging: <c>CREATE TEMP TABLE ... ON COMMIT DROP</c> plus binary COPY.
/// The target role therefore needs the TEMP privilege on the target database
/// (<c>GRANT TEMP ON DATABASE ... TO ...</c>; PUBLIC has it by default).
/// Must run inside an open transaction on the context connection (Npgsql enlists commands
/// and COPY in the connection's current transaction).
/// </summary>
internal static partial class SsBulkStaging
{
    [GeneratedRegex("^[a-z_][a-z0-9_]{0,62}$")]
    private static partial Regex SafeName();

    internal static async Task CreateAsync<T>(
        NpgsqlConnection connection,
        string table,
        IReadOnlyList<SsBulkColumn<T>> columns,
        CancellationToken ct
    )
    {
        Guard(table);
        foreach (var column in columns)
            Guard(column.Name);
        var definition = string.Join(", ", columns.Select(x => $"{x.Name} {x.SqlType}"));
        await using var command = new NpgsqlCommand(
            $"DROP TABLE IF EXISTS pg_temp.{table}; CREATE TEMP TABLE {table} ({definition}) ON COMMIT DROP",
            connection
        );
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Creates the staging table and binary-copies <paramref name="rows"/> into it.</summary>
    internal static async Task<ulong> StageAsync<T>(
        NpgsqlConnection connection,
        string table,
        IReadOnlyList<SsBulkColumn<T>> columns,
        IEnumerable<T> rows,
        CancellationToken ct
    )
    {
        await CreateAsync(connection, table, columns, ct);
        await using var importer = await connection.BeginBinaryImportAsync(
            $"COPY {table} ({string.Join(", ", columns.Select(x => x.Name))}) FROM STDIN (FORMAT BINARY)",
            ct
        );
        foreach (var row in rows)
        {
            await importer.StartRowAsync(ct);
            foreach (var column in columns)
            {
                var value = column.Value(row);
                if (value is null)
                    await importer.WriteNullAsync(ct);
                else
                    await importer.WriteAsync(value, column.DbType, ct);
            }
        }
        return await importer.CompleteAsync(ct);
    }

    internal static async Task<int> ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken ct,
        params NpgsqlParameter[] parameters
    )
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        return await command.ExecuteNonQueryAsync(ct);
    }

    internal static async Task<List<TResult>> QueryAsync<TResult>(
        NpgsqlConnection connection,
        string sql,
        Func<NpgsqlDataReader, TResult> map,
        CancellationToken ct,
        params NpgsqlParameter[] parameters
    )
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<TResult>();
        while (await reader.ReadAsync(ct))
            result.Add(map(reader));
        return result;
    }

    /// <summary>
    /// Pre-allocates identity values from the column's own sequence. Rows are then inserted with
    /// explicit ids, so generated keys map to source keys by explicit join, never RETURNING order.
    /// </summary>
    internal static async Task<long[]> AllocateIdsAsync(
        NpgsqlConnection connection,
        string table,
        int count,
        CancellationToken ct,
        string column = "Id"
    )
    {
        if (count == 0)
            return [];
        return (
            await QueryAsync(
                connection,
                "SELECT nextval(pg_get_serial_sequence(@table, @column)) FROM generate_series(1, @count)",
                reader => reader.GetInt64(0),
                ct,
                new NpgsqlParameter("table", $"public.\"{table}\""),
                new NpgsqlParameter("column", column),
                new NpgsqlParameter("count", count)
            )
        ).ToArray();
    }

    private static void Guard(string identifier)
    {
        if (!SafeName().IsMatch(identifier))
            throw new ArgumentException("Unsafe staging identifier.", nameof(identifier));
    }
}
