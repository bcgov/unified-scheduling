using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Unified.DataMigration.Services;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.DataMigration;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.DataMigration;

public sealed class SsReplayPrefetchTests : IAsyncDisposable
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly PageReadCounter reads = new();
    private readonly UnifiedDbContext db;

    public SsReplayPrefetchTests()
    {
        connection.Open();
        db = new SqliteTestUnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(connection).AddInterceptors(reads).Options
        );
        db.Database.EnsureCreated();
    }

    private const int GroupCount = 4;
    private static int PageCount => SsReplayPrefetch.PagesPerType + 1;

    private static Dictionary<string, List<SsRow>> Rows() =>
        new()
        {
            ["Shift"] = Enumerable
                .Range(0, PageCount * SsReplayPrefetch.PageSize)
                .Select(id => new SsRow(new Dictionary<string, object?> { ["Id"] = id.ToString() }))
                .ToList(),
        };

    private static List<SsRow> Group(Dictionary<string, List<SsRow>> rows, int group) =>
        Enumerable.Range(0, PageCount).Select(page => rows["Shift"][page * SsReplayPrefetch.PageSize + group]).ToList();

    private async Task SeedAsync(Dictionary<string, List<SsRow>> rows, CancellationToken ct)
    {
        // Groups span more pages than the cache can retain; group 3 has no crosswalks.
        for (var group = 0; group < GroupCount - 1; group++)
        {
            var members = Group(rows, group);
            for (var page = 0; page < members.Count; page++)
                db.DataMigrationRecords.Add(
                    new DataMigrationRecord
                    {
                        Source = "SS",
                        EntityType = "Shift",
                        LegacyId = members[page].Id,
                        TargetType = "ShiftEntry",
                        TargetKey = group == 2 && page == 1 ? "split" : $"target-{group}",
                        PayloadHash = group == 1 && page == 1 ? "changed" : $"hash-{group}",
                    }
                );
        }
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        reads.Count = 0;
    }

    [Fact]
    public async Task InterleavedGroups_LoadEachSourcePageAtMostOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var rows = Rows();
        await SeedAsync(rows, ct);
        using var prefetch = new SsReplayPrefetch(db, rows);

        for (var group = 0; group < GroupCount; group++)
        foreach (var member in Group(rows, group))
        {
            var (known, entry) = await prefetch.TryGetAsync("Shift", member.Id, true, ct);
            var expected = await db.DataMigrationRecords.AsNoTracking().SingleOrDefaultAsync(
                x => x.Source == "SS" && x.EntityType == "Shift" && x.LegacyId == member.Id,
                ct
            );
            if (known)
            {
                Assert.Equal(expected?.TargetKey, entry?.TargetKey);
                Assert.Equal(expected?.PayloadHash, entry?.PayloadHash);
            }
        }

        Assert.Equal(PageCount, prefetch.PageLoads);
        Assert.Equal(PageCount, reads.Count); // Actual EF page queries, not just the cache's own counter.
        Assert.True(prefetch.Hits > 0);
        Assert.True(prefetch.Fallbacks > 0);
        Assert.False((await prefetch.TryGetAsync("Shift", Group(rows, 1)[0].Id, true, ct)).Known);
        Assert.Equal(PageCount, reads.Count);
    }

    [Fact]
    public async Task InterleavedGroups_MatchFullPathForUnchangedMissingChangedAndSplitGroups()
    {
        var ct = TestContext.Current.CancellationToken;
        var rows = Rows();
        await SeedAsync(rows, ct);

        async Task<(List<string> Verdicts, List<string> Outcomes)> RunAsync(bool enabled)
        {
            db.ChangeTracker.Clear();
            var session = new SsWriteSession(
                db,
                new DataMigrationRun { Source = "SS", Job = "Scheduling", Status = "Running" }
            )
            {
                PrefetchReplayRecords = enabled,
            };
            session.BeginReplayPrefetch(rows);
            var verdicts = new List<string>();
            try
            {
                for (var group = 0; group < GroupCount; group++)
                {
                    try
                    {
                        var replayed = await session.ReplayGroupAsync(
                            "Shift",
                            Group(rows, group),
                            $"hash-{group}",
                            _ => throw new InvalidOperationException("Unexpected update."),
                            ct
                        );
                        verdicts.Add(replayed ? "unchanged" : "missing");
                    }
                    catch (SsReject error)
                    {
                        verdicts.Add(error.Message);
                    }
                }
            }
            finally
            {
                session.EndReplayPrefetch();
            }
            return (
                verdicts,
                session.Outcomes.Select(x => $"{x.EntityType}:{x.LegacyId}:{x.Outcome}:{x.Reason}").Order().ToList()
            );
        }

        var full = await RunAsync(false);
        Assert.Equal(0, reads.Count);
        var fast = await RunAsync(true);

        Assert.Equal(new[] { "unchanged", "LinkedOnlyTargetCannotUpdate", "GroupMergeRequiresMultiTargetRepartition", "missing" }, full.Verdicts);
        Assert.Equal(full.Verdicts, fast.Verdicts);
        Assert.Equal(full.Outcomes, fast.Outcomes);
        Assert.Equal(PageCount, reads.Count);
    }

    [Fact]
    public async Task DuplicateCrosswalk_NeverGetsAnOptimisticCachedVerdict()
    {
        var ct = TestContext.Current.CancellationToken;
        await db.Database.ExecuteSqlRawAsync(
            """DROP INDEX "IX_DataMigrationRecords_Source_EntityType_LegacyId" """,
            ct
        );
        db.DataMigrationRecords.AddRange(
            Enumerable.Range(0, 2).Select(_ => new DataMigrationRecord
            {
                Source = "SS",
                EntityType = "Shift",
                LegacyId = "1",
                TargetType = "ShiftEntry",
                TargetKey = "1",
                PayloadHash = "hash",
            })
        );
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        var rows = new Dictionary<string, List<SsRow>>
        {
            ["Shift"] = [new SsRow(new Dictionary<string, object?> { ["Id"] = "1" })],
        };
        using var prefetch = new SsReplayPrefetch(db, rows);

        Assert.False((await prefetch.TryGetAsync("Shift", "1", true, ct)).Known);
        Assert.False((await prefetch.TryGetAsync("Shift", "1", true, ct)).Known);
        Assert.Equal(1, prefetch.PageLoads);
        var session = new SsWriteSession(
            db,
            new DataMigrationRun { Source = "SS", Job = "Scheduling", Status = "Running" }
        )
        {
            PrefetchReplayRecords = true,
        };
        session.BeginReplayPrefetch(rows);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                session.ReplayAsync("Shift", rows["Shift"][0], "hash", false, ct)
            );
        }
        finally
        {
            session.EndReplayPrefetch();
        }
    }

    private sealed class PageReadCounter : DbCommandInterceptor
    {
        public int Count;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            if (
                command.CommandText.Contains("FROM \"DataMigrationRecords\"", StringComparison.Ordinal)
                && command.CommandText.Contains("\"SourceUpdatedAtUtc\"", StringComparison.Ordinal)
                && !command.CommandText.Contains("\"AfterHash\"", StringComparison.Ordinal)
            )
                Count++;
            return ValueTask.FromResult(result);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }
}
