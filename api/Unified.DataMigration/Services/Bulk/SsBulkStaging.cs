using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Unified.DataMigration.Options;

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

    private static readonly ConditionalWeakTable<NpgsqlConnection, StrongBox<int>> Timeouts = new();

    /// <summary>Upper bound for one bulk lock acquisition (chunk or undo) before it gives up.</summary>
    internal static TimeSpan LockWaitLimit { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Runs <paramref name="lockSql"/> (one or more <c>LOCK TABLE</c> statements, in the caller's order) inside the
    /// open transaction without ever becoming a deadlock participant that PostgreSQL resolves by aborting someone
    /// else. Each attempt is bounded well below <c>deadlock_timeout</c> and then rolls back to a savepoint,
    /// releasing every lock it took, so in any wait cycle the bulk caller backs off first and retries with jitter
    /// until <see cref="LockWaitLimit"/> elapses; then the last timeout error propagates.
    /// </summary>
    internal static async Task LockTablesAsync(NpgsqlConnection connection, string lockSql, CancellationToken ct)
    {
        var deadlockMs = (
            await QueryAsync(
                connection,
                "SELECT (extract(epoch FROM current_setting('deadlock_timeout')::interval) * 1000)::int",
                reader => reader.GetInt32(0),
                ct
            )
        ).Single();
        // Up to a few statements per attempt, each bounded by a quarter of deadlock_timeout.
        var attemptMs = Math.Max(10, deadlockMs / 4);
        var deadline = DateTimeOffset.UtcNow + LockWaitLimit;
        for (var attempt = 0; ; attempt++)
        {
            await ExecuteAsync(connection, $"SAVEPOINT ss_bulk_lock; SET LOCAL statement_timeout = {attemptMs}", ct);
            try
            {
                await ExecuteAsync(connection, lockSql, ct);
                await ExecuteAsync(
                    connection,
                    "SET LOCAL statement_timeout = DEFAULT; RELEASE SAVEPOINT ss_bulk_lock",
                    ct
                );
                return;
            }
            catch (PostgresException error)
                when (error.SqlState is PostgresErrorCodes.QueryCanceled or PostgresErrorCodes.LockNotAvailable
                    && !ct.IsCancellationRequested
                    && DateTimeOffset.UtcNow < deadline
                )
            {
                // Rolling back to the savepoint releases the locks of this attempt and the timeout setting.
                await ExecuteAsync(
                    connection,
                    "ROLLBACK TO SAVEPOINT ss_bulk_lock; RELEASE SAVEPOINT ss_bulk_lock",
                    ct
                );
                await Task.Delay(
                    TimeSpan.FromMilliseconds(Random.Shared.Next(20, 100 + 20 * Math.Min(attempt, 20))),
                    ct
                );
            }
        }
    }

    /// <summary>
    /// Returns the context's Npgsql connection and binds the context command timeout to it so raw
    /// commands and COPY operations issued by the bulk helpers inherit the migration timeout
    /// instead of Npgsql's connection-string default.
    /// </summary>
    internal static NpgsqlConnection Connection(DbContext db)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var seconds = db.Database.GetCommandTimeout() ?? DataMigrationOptions.DefaultCommandTimeoutSeconds;
        Timeouts.AddOrUpdate(connection, new StrongBox<int>(seconds));
        return connection;
    }

    /// <summary>Command timeout (seconds) bound to <paramref name="connection"/> via <see cref="Connection"/>.</summary>
    internal static int? CommandTimeout(NpgsqlConnection connection) =>
        Timeouts.TryGetValue(connection, out var box) ? box.Value : null;

    internal static NpgsqlCommand Command(NpgsqlConnection connection, string sql)
    {
        var command = new NpgsqlCommand(sql, connection);
        if (CommandTimeout(connection) is { } seconds)
            command.CommandTimeout = seconds;
        return command;
    }

    internal static async Task<NpgsqlBinaryImporter> ImportAsync(
        NpgsqlConnection connection,
        string copy,
        CancellationToken ct
    )
    {
        var importer = await connection.BeginBinaryImportAsync(copy, ct);
        if (CommandTimeout(connection) is { } seconds)
            importer.Timeout = TimeSpan.FromSeconds(seconds);
        return importer;
    }

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
        await using var command = Command(
            connection,
            $"DROP TABLE IF EXISTS pg_temp.{table}; CREATE TEMP TABLE {table} ({definition}) ON COMMIT DROP"
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
        await using var importer = await ImportAsync(
            connection,
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
        await using var command = Command(connection, sql);
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
        await using var command = Command(connection, sql);
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
