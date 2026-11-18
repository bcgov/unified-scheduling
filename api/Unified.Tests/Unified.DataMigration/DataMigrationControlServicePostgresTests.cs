using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Unified.DataMigration.Services;
using Unified.Db;
using Unified.Db.Migrations;
using Unified.Db.Models.DataMigration;

namespace Unified.Tests.DataMigration;

public sealed class DataMigrationControlServicePostgresTests : IAsyncLifetime
{
    private readonly string _schema = $"migration_test_{Guid.NewGuid():N}";
    private NpgsqlConnection _connection = null!;
    private UnifiedDbContext _db = null!;

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UNIFIED_TEST_POSTGRES_CONNECTION_STRING"));

    [Fact(
        Skip = "Set UNIFIED_TEST_POSTGRES_CONNECTION_STRING to run against PostgreSQL.",
        SkipUnless = nameof(IsConfigured)
    )]
    public async Task TryStartRunAsync_ActiveRunGuard_AllowsOnlyOneRunningRunPerSource()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new DataMigrationControlService(_db);
        var first = await service.TryStartRunAsync("SS", false, ct);
        var second = await service.TryStartRunAsync("SS", false, ct);

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Single(_db.ChangeTracker.Entries<DataMigrationRun>());
        Assert.NotNull(await service.TryStartRunAsync("CASS", false, ct));
        await service.CompleteRunAsync(first, "Completed", ct);
        Assert.NotNull(await service.TryStartRunAsync("SS", false, ct));
    }

    [Fact(
        Skip = "Set UNIFIED_TEST_POSTGRES_CONNECTION_STRING to run against PostgreSQL.",
        SkipUnless = nameof(IsConfigured)
    )]
    public async Task CompleteRunAsync_AfterFailedBatch_LoadsPersistedRunAndAllowsRestart()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new DataMigrationControlService(_db);
        var orchestrator = new DataMigrationOrchestrator(_db);
        var run = await service.TryStartRunAsync("SS", true, ct);
        Assert.NotNull(run);
        run.ScannedCount = 7;
        await _db.SaveChangesAsync(ct);
        var beforeBatch = await _db.DataMigrationRuns.AsNoTracking().SingleAsync(item => item.Id == run.Id, ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.ExecuteBatchAsync(
                async batchCt =>
                {
                    run.ScannedCount = 99;
                    _db.DataMigrationRecords.Add(CreateRecord());
                    await _db.SaveChangesAsync(batchCt);
                    throw new InvalidOperationException("batch failed");
                },
                ct
            )
        );

        Assert.Equal(EntityState.Detached, _db.Entry(run).State);
        Assert.Empty(await _db.DataMigrationRecords.ToListAsync(ct));
        await orchestrator.RecordFailureAsync(
            new DataMigrationFailure
            {
                RunId = run.Id,
                Source = "SS",
                ErrorType = "BatchFailure",
                ErrorMessage = "batch failed",
                OccurredOn = DateTimeOffset.UtcNow,
            },
            ct
        );
        await service.CompleteRunAsync(run, "Failed", ct);

        var saved = await _db.DataMigrationRuns.AsNoTracking().SingleAsync(item => item.Id == run.Id, ct);
        Assert.Equal("Failed", saved.Status);
        Assert.NotNull(saved.CompletedOn);
        Assert.Equal(beforeBatch.StartedOn, saved.StartedOn);
        Assert.True(saved.DryRun);
        Assert.Equal(7, saved.ScannedCount);
        Assert.Single(await _db.DataMigrationFailures.ToListAsync(ct));
        var restarted = await service.TryStartRunAsync("SS", false, ct);
        Assert.NotNull(restarted);
        Assert.NotEqual(run.Id, restarted.Id);
    }

    [Theory(
        Skip = "Set UNIFIED_TEST_POSTGRES_CONNECTION_STRING to run against PostgreSQL.",
        SkipUnless = nameof(IsConfigured)
    )]
    [InlineData(true, PostgresErrorCodes.StringDataRightTruncation)]
    [InlineData(false, PostgresErrorCodes.UndefinedTable)]
    public async Task TryStartRunAsync_NonContentionDatabaseError_Propagates(bool overlengthSource, string sqlState)
    {
        var ct = TestContext.Current.CancellationToken;
        if (!overlengthSource)
        {
            await _db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "DataMigrationRuns" RENAME TO "UnavailableRuns";""",
                ct
            );
        }

        var service = new DataMigrationControlService(_db);

        var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.TryStartRunAsync(overlengthSource ? new string('S', 17) : "SS", false, ct)
        );

        Assert.Equal(sqlState, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact(
        Skip = "Set UNIFIED_TEST_POSTGRES_CONNECTION_STRING to run against PostgreSQL.",
        SkipUnless = nameof(IsConfigured)
    )]
    public async Task TryStartRunAsync_AnotherTrackedEntityViolatesUniqueIndex_Propagates()
    {
        var ct = TestContext.Current.CancellationToken;
        _db.DataMigrationRecords.Add(CreateRecord());
        await _db.SaveChangesAsync(ct);
        _db.DataMigrationRecords.Add(CreateRecord());
        var service = new DataMigrationControlService(_db);

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => service.TryStartRunAsync("SS", false, ct));

        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("IX_DataMigrationRecords_Source_EntityType_LegacyId", postgres.ConstraintName);
    }

    public async ValueTask InitializeAsync()
    {
        if (!IsConfigured)
        {
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        _connection = new NpgsqlConnection(
            Environment.GetEnvironmentVariable("UNIFIED_TEST_POSTGRES_CONNECTION_STRING")
        );
        await _connection.OpenAsync(ct);
        await using (
            var command = new NpgsqlCommand(
                $"CREATE SCHEMA \"{_schema}\"; SET search_path TO \"{_schema}\";",
                _connection
            )
        )
        {
            await command.ExecuteNonQueryAsync(ct);
        }

        _db = new UnifiedDbContext(new DbContextOptionsBuilder<UnifiedDbContext>().UseNpgsql(_connection).Options);
        var commands = _db.GetService<IMigrationsSqlGenerator>()
            .Generate(new AddDataMigrationState().UpOperations, _db.Model);
        foreach (var command in commands)
        {
            await _db.Database.ExecuteSqlRawAsync(command.CommandText, ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
        if (_connection is not null)
        {
            await using var command = new NpgsqlCommand($"DROP SCHEMA \"{_schema}\" CASCADE;", _connection);
            await command.ExecuteNonQueryAsync(CancellationToken.None);
            await _connection.DisposeAsync();
        }
    }

    private static DataMigrationRecord CreateRecord() =>
        new()
        {
            Source = "SS",
            EntityType = "User",
            LegacyId = "1",
            TargetType = "User",
            TargetKey = "1",
            PayloadHash = "A",
        };
}
