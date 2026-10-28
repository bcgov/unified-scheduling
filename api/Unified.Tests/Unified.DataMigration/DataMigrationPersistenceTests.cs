using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Unified.DataMigration.Services;
using Unified.Db;
using Unified.Db.Models.DataMigration;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.DataMigration;

public sealed class DataMigrationPersistenceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:;Foreign Keys=True");
    private readonly UnifiedDbContext _db;

    public DataMigrationPersistenceTests()
    {
        _connection.Open();
        _db = new SqliteTestUnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(_connection).Options
        );
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task RecordIdentity_IsUniquePerSourceEntityAndLegacyId()
    {
        _db.DataMigrationRecords.Add(CreateRecord("SS", "User", "1"));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _db.DataMigrationRecords.Add(CreateRecord("SS", "User", "1"));

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FailedBatch_RollsBackWritesAndPersistsFailureEvidence()
    {
        var run = new DataMigrationRun { Source = "SS", Status = "Running" };
        _db.DataMigrationRuns.Add(run);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var orchestrator = new DataMigrationOrchestrator(_db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.ExecuteBatchAsync(
                async _ =>
                {
                    _db.DataMigrationRecords.Add(CreateRecord("SS", "User", "2"));
                    await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
                    throw new InvalidOperationException("batch failed");
                },
                TestContext.Current.CancellationToken
            )
        );

        await orchestrator.RecordFailureAsync(
            new DataMigrationFailure
            {
                RunId = run.Id,
                Source = "SS",
                ErrorType = "BatchFailure",
                ErrorMessage = "batch failed",
                OccurredOn = DateTimeOffset.UtcNow,
            },
            TestContext.Current.CancellationToken
        );

        Assert.Empty(await _db.DataMigrationRecords.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await _db.DataMigrationFailures.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WatermarkIdentity_IsUniquePerSourceAndEntityType()
    {
        _db.DataMigrationWatermarks.Add(
            new DataMigrationWatermark
            {
                Source = "SS",
                EntityType = "User",
                SourceChangedAtUtc = DateTimeOffset.UtcNow,
                LegacyId = "1",
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _db.DataMigrationWatermarks.Add(
            new DataMigrationWatermark
            {
                Source = "SS",
                EntityType = "User",
                SourceChangedAtUtc = DateTimeOffset.UtcNow,
                LegacyId = "2",
            }
        );

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Watermark_LastCompletedRunId_RejectsUnknownRun()
    {
        _db.DataMigrationWatermarks.Add(
            new DataMigrationWatermark
            {
                Source = "SS",
                EntityType = "User",
                SourceChangedAtUtc = DateTimeOffset.UtcNow,
                LegacyId = "1",
                LastCompletedRunId = Guid.NewGuid(),
            }
        );

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ActiveRunGuard_AllowsOnlyOneRunningRunPerSource()
    {
        var service = new DataMigrationControlService(_db);
        var first = await service.TryStartRunAsync("SS", false, TestContext.Current.CancellationToken);
        var second = await service.TryStartRunAsync("SS", false, TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.Null(second);
        await service.CompleteRunAsync(first!, "Completed", TestContext.Current.CancellationToken);
        Assert.NotNull(await service.TryStartRunAsync("SS", false, TestContext.Current.CancellationToken));
    }

    private static DataMigrationRecord CreateRecord(string source, string entityType, string legacyId) =>
        new()
        {
            Source = source,
            EntityType = entityType,
            LegacyId = legacyId,
            TargetType = "User",
            TargetKey = legacyId,
            PayloadHash = "A",
        };

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
