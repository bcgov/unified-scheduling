using Microsoft.EntityFrameworkCore;
using Npgsql;
using Unified.DataMigration.Services;
using Unified.Db;
using Unified.Db.Models.DataMigration;

namespace Unified.Tests.DataMigration;

public sealed class DataMigrationControlServicePostgresTests : IAsyncLifetime
{
    private readonly string _database = $"migration_test_{Guid.NewGuid():N}";
    private UnifiedDbContext _db = null!;

    private static string? AdminConnectionString =>
        Environment.GetEnvironmentVariable("UNIFIED_TEST_POSTGRES_CONNECTION_STRING");

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(AdminConnectionString);

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

        // Fresh database per test, built with every current migration exactly as MigrationAndSeedService does.
        var ct = TestContext.Current.CancellationToken;
        await using (var admin = new NpgsqlConnection(AdminConnectionString))
        {
            await admin.OpenAsync(ct);
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_database}\";", admin);
            await command.ExecuteNonQueryAsync(ct);
        }

        _db = new UnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>()
                .UseNpgsql(
                    new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = _database }.ConnectionString
                )
                .Options
        );
        await _db.Database.MigrateAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
        if (IsConfigured)
        {
            await using var admin = new NpgsqlConnection(AdminConnectionString);
            await admin.OpenAsync(CancellationToken.None);
            await using var command = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE);",
                admin
            );
            await command.ExecuteNonQueryAsync(CancellationToken.None);
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
