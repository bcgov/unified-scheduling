using Microsoft.EntityFrameworkCore;
using Npgsql;
using Unified.DataMigration.Services;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.DataMigration;

namespace Unified.Tests.DataMigration;

// Batch commits flush only touched outcomes, keep running counts and detach the tracker.
// The persisted evidence must not depend on the batch size. Isolated disposable PostgreSQL only.
public sealed class SsBatchProgressPostgresTests : IAsyncLifetime
{
    private const string Variable = "UNIFIED_MIGRATION_ISOLATED_TEST_CONNECTION_STRING";
    private const string SkipReason = "Set UNIFIED_MIGRATION_ISOLATED_TEST_CONNECTION_STRING for isolated PostgreSQL.";
    private static string? Admin => Environment.GetEnvironmentVariable(Variable);
    public static bool IsConfigured => !string.IsNullOrWhiteSpace(Admin);
    private readonly List<string> databases = [];

    public ValueTask InitializeAsync()
    {
        if (IsConfigured)
        {
            var admin = new NpgsqlConnectionStringBuilder(Admin);
            if (
                admin.Host != "127.0.0.1"
                || !admin.Database!.StartsWith("unified_migration_isolated_", StringComparison.Ordinal)
            )
                throw new InvalidOperationException("An isolated loopback PostgreSQL instance is required.");
        }
        return ValueTask.CompletedTask;
    }

    private static SsRow Row(int id, int? type, DateTime created, DateTime? updated) =>
        new(
            new Dictionary<string, object?>
            {
                ["Id"] = id,
                ["Type"] = type,
                ["CreatedOn"] = created,
                ["UpdatedOn"] = updated,
            }
        );

    private static Dictionary<string, List<SsRow>> Snapshot()
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return new()
        {
            ["LookupCode"] = Enumerable
                .Range(1, 8)
                .Select(i => Row(i, i == 4 ? 1 : 6, start.AddDays(i), i % 3 == 0 ? start.AddDays(40 + i) : null))
                .ToList(),
            ["SheriffTraining"] = Enumerable
                .Range(1, 36)
                .Select(i => Row(i, null, start.AddDays(i), i % 4 == 0 ? start.AddDays(60 + i) : null))
                .ToList(),
            ["User"] = [Row(1, null, start, null)],
        };
    }

    // A deterministic mix of migrated, excluded, rejected (rolled back), failed and revisited rows.
    private static Task Write(SsWriteSession session, string type, SsRow row)
    {
        var id = int.Parse(row.Id, System.Globalization.CultureInfo.InvariantCulture);
        if (type == "Training")
        {
            session.Outcome(type, row.Id, id == 2 ? "NeedsAction" : "Migrated", id == 5 ? "ExistingCrosswalk" : null);
            return Task.CompletedTask;
        }
        if (id % 7 == 0)
        {
            // Revisit outcomes that earlier batches already flushed (row 35 then rolls these back).
            var earlier = (id - 4).ToString(System.Globalization.CultureInfo.InvariantCulture);
            session.Outcome("SheriffTraining", earlier, "Migrated", "UnchangedMappedSource");
            session.Outcome("Training", "1", "Migrated", "Revisited" + id);
        }
        if (id % 5 == 0)
        {
            session.Outcome(type, row.Id, "Migrated");
            session.Outcome("TrainingValidation", row.Id, "NeedsAction", "RolledBackAux");
            throw new SsReject("SyntheticReject");
        }
        if (id % 9 == 0)
            throw new InvalidOperationException("synthetic");
        if (id % 4 == 0)
        {
            session.Outcome("TrainingValidation", row.Id, "NeedsAction", "Aux");
            session.Outcome(type, row.Id, "Excluded", "BeforeHistoryBoundary");
            return Task.CompletedTask;
        }
        session.Outcome(type, row.Id, "Migrated", id % 6 == 0 ? "ExistingCrosswalk" : null);
        return Task.CompletedTask;
    }

    private async Task<string> NewDatabaseAsync(CancellationToken ct)
    {
        var name = $"migration_it_{Guid.NewGuid():N}";
        var admin = new NpgsqlConnectionStringBuilder(Admin) { Pooling = false };
        await using var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await command.ExecuteNonQueryAsync(ct);
        databases.Add(name);
        return new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = name }.ConnectionString;
    }

    private static UnifiedDbContext Context(string connection) =>
        new(new DbContextOptionsBuilder<UnifiedDbContext>().UseNpgsql(connection).Options);

    private static void AssertCountsMatchFullScan(DataMigrationRun persisted, SsWriteSession session)
    {
        var expected = new DataMigrationRun { Source = "SS", Status = "Running" };
        SsBatchProgress.SetCounts(expected, session);
        Assert.Equal(
            (
                expected.ScannedCount,
                expected.InsertedCount,
                expected.UpdatedCount,
                expected.UnchangedCount,
                expected.SkippedCount,
                expected.RejectedCount,
                expected.FailedCount
            ),
            (
                persisted.ScannedCount,
                persisted.InsertedCount,
                persisted.UpdatedCount,
                persisted.UnchangedCount,
                persisted.SkippedCount,
                persisted.RejectedCount,
                persisted.FailedCount
            )
        );
    }

    private static async Task AssertPersistedOutcomesMatchAsync(
        UnifiedDbContext observer,
        SsWriteSession session,
        CancellationToken ct
    )
    {
        var persisted = await observer
            .DataMigrationOutcomes.AsNoTracking()
            .Where(x => x.RunId == session.Run.Id)
            .Select(x => new
            {
                x.EntityType,
                x.LegacyId,
                x.Outcome,
                x.Reason,
            })
            .ToListAsync(ct);
        Assert.Equal(
            session
                .Outcomes.Select(x => new
                {
                    x.EntityType,
                    x.LegacyId,
                    x.Outcome,
                    x.Reason,
                })
                .OrderBy(x => x.EntityType, StringComparer.Ordinal)
                .ThenBy(x => x.LegacyId, StringComparer.Ordinal),
            persisted.OrderBy(x => x.EntityType, StringComparer.Ordinal).ThenBy(x => x.LegacyId, StringComparer.Ordinal)
        );
    }

    private async Task<string> RunAsync(int batchSize, CancellationToken ct)
    {
        var connection = await NewDatabaseAsync(ct);
        await using var db = Context(connection);
        await db.Database.MigrateAsync(ct);
        var run = new DataMigrationRun
        {
            Source = "SS",
            Job = "Training",
            Status = "Running",
            StartedOn = DateTimeOffset.UtcNow,
        };
        db.DataMigrationRuns.Add(run);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        run = await db.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == run.Id, ct);
        var session = new SsWriteSession(db, run);
        var snapshot = Snapshot();
        var commits = 0;
        await using var observer = Context(connection);
        await using (
            var batches = new SsBatchProgress(
                db,
                session,
                snapshot,
                new DateOnly(2020, 1, 1),
                batchSize,
                _ => Task.CompletedTask
            )
        )
        {
            await batches.BeginAsync(ct);
            foreach (var (table, type) in new[] { ("LookupCode", "Training"), ("SheriffTraining", "SheriffTraining") })
            foreach (var row in snapshot[table].Where(x => table != "LookupCode" || x.Int("Type") == 6))
            {
                await session.RowAsync(type, row, () => Write(session, type, row), ct);
                var persisted = await observer.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == run.Id, ct);
                if (persisted.CommittedBatchCount == commits)
                    continue;
                // Just committed: evidence and counters equal a full scan, and nothing stays tracked.
                commits = persisted.CommittedBatchCount;
                AssertCountsMatchFullScan(persisted, session);
                await AssertPersistedOutcomesMatchAsync(observer, session, ct);
                Assert.Empty(db.ChangeTracker.Entries());
            }
            await batches.CommitAsync(true, ct);
        }
        var final = await observer.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == run.Id, ct);
        AssertCountsMatchFullScan(final, session);
        await AssertPersistedOutcomesMatchAsync(observer, session, ct);
        if (batchSize == 1)
            Assert.True(commits > 20);
        var workItems = await observer
            .DataMigrationWorkItems.AsNoTracking()
            .OrderBy(x => x.EntityType)
            .ThenBy(x => x.LegacyId)
            .Select(x => new
            {
                x.Job,
                x.Stream,
                x.EntityType,
                x.LegacyId,
                x.Outcome,
                x.Reason,
                x.PendingRetry,
                x.Attempts,
                x.SourceChangedAtUtc,
            })
            .ToListAsync(ct);
        // Checkpoint LegacyId/SourceChangedAtUtc never regress, so they legitimately depend on
        // where batch boundaries fall; the remaining checkpoint fields do not.
        var checkpoints = await observer
            .DataMigrationCheckpoints.AsNoTracking()
            .OrderBy(x => x.EntityType)
            .Select(x => new
            {
                x.EntityType,
                x.IdKind,
                x.UpperLegacyId,
                x.UpperChangedAtUtc,
            })
            .ToListAsync(ct);
        var outcomes = session.Outcomes.Select(x => (x.EntityType, x.LegacyId, x.Outcome, x.Reason));
        return System.Text.Json.JsonSerializer.Serialize(
            new
            {
                final.Status,
                final.ScannedCount,
                final.UnchangedCount,
                final.SkippedCount,
                final.RejectedCount,
                final.FailedCount,
                Outcomes = outcomes.Select(x => $"{x.EntityType}:{x.LegacyId}:{x.Outcome}:{x.Reason}"),
                workItems,
                checkpoints,
            }
        );
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsConfigured))]
    public async Task BatchCommits_PersistTheSameEvidenceForEveryBatchSize()
    {
        var ct = TestContext.Current.CancellationToken;
        var single = await RunAsync(1, ct);
        Assert.Contains("\"Status\":\"Failed\"", single, StringComparison.Ordinal);
        Assert.Equal(single, await RunAsync(4, ct));
        Assert.Equal(single, await RunAsync(1000, ct));
    }

    public async ValueTask DisposeAsync()
    {
        if (databases.Count == 0)
            return;
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(Admin) { Pooling = false }.ConnectionString
        );
        await connection.OpenAsync(CancellationToken.None);
        foreach (var name in databases)
        {
            await using var command = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
