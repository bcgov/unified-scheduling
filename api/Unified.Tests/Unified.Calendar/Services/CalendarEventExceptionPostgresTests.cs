using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Unified.Db;
using Unified.Db.Migrations;

namespace Unified.Tests.Calendar.Services;

public sealed class CalendarEventExceptionPostgresTests
{
    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UNIFIED_TEST_POSTGRES_CONNECTION_STRING"));

    [Fact(
        Skip = "Set UNIFIED_TEST_POSTGRES_CONNECTION_STRING to run against PostgreSQL.",
        SkipUnless = nameof(IsConfigured)
    )]
    public async Task Migration_BackfillsIndependentReasonsAndMembershipQueriesExecuteAgainstPostgres()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(
            Environment.GetEnvironmentVariable("UNIFIED_TEST_POSTGRES_CONNECTION_STRING")
        );
        await connection.OpenAsync(ct);
        await using var db = new UnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseNpgsql(connection).Options
        );
        await CreateLegacyEventsAsync(db);
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "Events" VALUES
              (1, 42, '2026-06-01 09:00Z', '2026-06-01 17:00Z', '2026-06-01 09:00Z', '2026-06-01 17:00Z', false, NULL),
              (2, 42, '2026-06-01 10:00Z', '2026-06-01 17:00Z', '2026-06-01 09:00Z', '2026-06-01 17:00Z', false, NULL),
              (3, 42, '2026-06-01 10:00Z', '2026-06-01 17:00Z', '2026-06-01 09:00Z', '2026-06-01 17:00Z', true, 'IsAway'),
              (4, NULL, '2026-06-01 09:00Z', '2026-06-01 17:00Z', NULL, NULL, true, 'IsAway'),
              (5, NULL, '2026-06-01 09:00Z', NULL, NULL, NULL, false, NULL),
              (6, 42, '2026-06-01 09:00Z', NULL, '2026-06-01 09:00Z', NULL, false, NULL),
              (7, 42, '2026-06-01 09:00Z', NULL, '2026-06-01 09:00Z', '2026-06-01 17:00Z', true, NULL),
              (8, 42, '2026-06-01 09:00Z', '2026-06-01 17:00Z', '2026-06-01 09:00Z', '2026-06-01 17:00Z', true, 'IsAway');
            """,
            ct
        );

        var migration = new RefactorCalendarEventExceptionReasonCodes();
        await ApplyAsync(db, migration.UpOperations);
        Assert.Null(
            db.Model.FindEntityType(typeof(global::Unified.Db.Models.Calendar.Event))!.FindProperty("IsException")
        );
        var reasons = await db
            .Events.OrderBy(item => item.Id)
            .Select(item => item.ExceptionReasonCodes)
            .ToArrayAsync(ct);
        Assert.Empty(reasons[0]);
        Assert.Equal(new[] { "DateTimeChanged" }, reasons[1]);
        Assert.Equal(new[] { "DateTimeChanged", "IsAway" }, reasons[2]);
        Assert.Equal(new[] { "IsAway" }, reasons[3]);
        Assert.Empty(reasons[4]);
        Assert.Empty(reasons[5]);
        Assert.Equal(new[] { "DateTimeChanged" }, reasons[6]);
        Assert.Equal(new[] { "IsAway" }, reasons[7]);
        Assert.Equal(
            new[] { 3, 4, 8 },
            await db
                .Events.Where(item => item.ExceptionReasonCodes.Contains("IsAway"))
                .OrderBy(item => item.Id)
                .Select(item => item.Id)
                .ToArrayAsync(ct)
        );
        Assert.Equal(
            new[] { 1, 2, 5, 6, 7 },
            await db
                .Events.Where(item => !item.ExceptionReasonCodes.Contains("IsAway"))
                .OrderBy(item => item.Id)
                .Select(item => item.Id)
                .ToArrayAsync(ct)
        );

        await ApplyAsync(db, migration.DownOperations);
        await using (
            var command = new NpgsqlCommand(
                "SELECT \"IsException\" AND \"ExceptionReason\" = 'IsAway' FROM \"Events\" WHERE \"Id\" = 3",
                connection
            )
        )
            Assert.Equal(true, await command.ExecuteScalarAsync(ct));
        await ApplyAsync(db, migration.UpOperations);
        Assert.Equal(
            reasons,
            await db.Events.OrderBy(item => item.Id).Select(item => item.ExceptionReasonCodes).ToArrayAsync(ct)
        );
    }

    [Theory(
        Skip = "Set UNIFIED_TEST_POSTGRES_CONNECTION_STRING to run against PostgreSQL.",
        SkipUnless = nameof(IsConfigured)
    )]
    [InlineData("Other")]
    [InlineData("None")]
    [InlineData("")]
    public async Task Migration_UnknownLegacyReason_AbortsWithoutDroppingData(string reason)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(
            Environment.GetEnvironmentVariable("UNIFIED_TEST_POSTGRES_CONNECTION_STRING")
        );
        await connection.OpenAsync(ct);
        await using var db = new UnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseNpgsql(connection).Options
        );
        await CreateLegacyEventsAsync(db);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "Events" VALUES (1, NULL, '2026-06-01 09:00Z', NULL, NULL, NULL, true, {reason});
            """,
            ct
        );

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            ApplyAsync(db, new RefactorCalendarEventExceptionReasonCodes().UpOperations)
        );
        Assert.Contains("Unmapped Events.ExceptionReason values", error.MessageText);
        Assert.Contains($"'{reason}'", error.MessageText);
        await using var command = new NpgsqlCommand(
            """
            SELECT "IsException", "ExceptionReason", to_jsonb(event_row) ? 'ExceptionReasonCodes'
            FROM "Events" event_row WHERE "Id" = 1
            """,
            connection
        );
        await using var reader = await command.ExecuteReaderAsync(ct);
        Assert.True(await reader.ReadAsync(ct));
        Assert.True(reader.GetBoolean(0));
        Assert.Equal(reason, reader.GetString(1));
        Assert.False(reader.GetBoolean(2));
    }

    [Theory(
        Skip = "Set UNIFIED_TEST_POSTGRES_CONNECTION_STRING to run against PostgreSQL.",
        SkipUnless = nameof(IsConfigured)
    )]
    [InlineData(false, "IsAway", null, false)]
    [InlineData(false, "IsAway", 42, false)]
    [InlineData(false, "IsAway", 42, true)]
    [InlineData(true, null, null, false)]
    [InlineData(true, null, null, true)]
    [InlineData(true, null, 42, false)]
    public async Task Migration_AmbiguousLegacyState_IdentifiesRecordAndPreservesLegacyColumnsAndData(
        bool isException,
        string? reason,
        int? seriesId,
        bool timeDeviation
    )
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(
            Environment.GetEnvironmentVariable("UNIFIED_TEST_POSTGRES_CONNECTION_STRING")
        );
        await connection.OpenAsync(ct);
        await using var db = new UnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseNpgsql(connection).Options
        );
        await CreateLegacyEventsAsync(db);
        var baseline = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
        var start = timeDeviation ? baseline.AddHours(1) : baseline;
        var end = baseline.AddHours(8);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "Events" VALUES
              (101, {seriesId}::integer, {start}, {end}, {baseline}, {end}, {isException}, {reason}::text),
              (202, 42, '2026-06-01 10:00Z', '2026-06-01 17:00Z', '2026-06-01 09:00Z', '2026-06-01 17:00Z', false, NULL);
            """,
            ct
        );

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            ApplyAsync(db, new RefactorCalendarEventExceptionReasonCodes().UpOperations)
        );
        var inconsistency = isException
            ? "IsException=true without a recognized reason or applicable date/time deviation"
            : "IsException=false with IsAway";
        Assert.Contains($"{inconsistency} (event IDs: 101)", error.MessageText);
        Assert.DoesNotContain("202", error.MessageText);
        await using var command = new NpgsqlCommand(
            """
            SELECT "Id", "IsException", "ExceptionReason", "EventSeriesId", "StartAtUtc", "EndAtUtc",
                   "SeriesStartAtUtc", "SeriesEndAtUtc", to_jsonb(event_row) ? 'ExceptionReasonCodes'
            FROM "Events" event_row ORDER BY "Id"
            """,
            connection
        );
        await using var reader = await command.ExecuteReaderAsync(ct);
        Assert.True(await reader.ReadAsync(ct));
        Assert.Equal(101, reader.GetInt32(0));
        Assert.Equal(isException, reader.GetBoolean(1));
        Assert.Equal(reason, reader.IsDBNull(2) ? null : reader.GetString(2));
        Assert.Equal(seriesId, reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3));
        Assert.Equal(start, reader.GetFieldValue<DateTimeOffset>(4));
        Assert.Equal(end, reader.GetFieldValue<DateTimeOffset>(5));
        Assert.Equal(baseline, reader.GetFieldValue<DateTimeOffset>(6));
        Assert.Equal(end, reader.GetFieldValue<DateTimeOffset>(7));
        Assert.False(reader.GetBoolean(8));
        Assert.True(await reader.ReadAsync(ct));
        Assert.Equal(202, reader.GetInt32(0));
        Assert.False(reader.GetBoolean(1));
        Assert.True(reader.IsDBNull(2));
        Assert.Equal(42, reader.GetInt32(3));
        Assert.Equal(baseline.AddHours(1), reader.GetFieldValue<DateTimeOffset>(4));
        Assert.Equal(end, reader.GetFieldValue<DateTimeOffset>(5));
        Assert.Equal(baseline, reader.GetFieldValue<DateTimeOffset>(6));
        Assert.Equal(end, reader.GetFieldValue<DateTimeOffset>(7));
        Assert.False(reader.GetBoolean(8));
        Assert.False(await reader.ReadAsync(ct));
    }

    private static async Task CreateLegacyEventsAsync(UnifiedDbContext db) =>
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TEMP TABLE "Events" (
                "Id" integer PRIMARY KEY, "EventSeriesId" integer,
                "StartAtUtc" timestamptz NOT NULL, "EndAtUtc" timestamptz,
                "SeriesStartAtUtc" timestamptz, "SeriesEndAtUtc" timestamptz,
                "IsException" boolean NOT NULL DEFAULT false, "ExceptionReason" text
            );
            """,
            TestContext.Current.CancellationToken
        );

    private static async Task ApplyAsync(
        UnifiedDbContext db,
        IReadOnlyList<Microsoft.EntityFrameworkCore.Migrations.Operations.MigrationOperation> operations
    )
    {
        var ct = TestContext.Current.CancellationToken;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var commands = db.GetService<IMigrationsSqlGenerator>().Generate(operations, db.Model);
        foreach (var migrationCommand in commands)
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = migrationCommand.CommandText;
            await command.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }
}
