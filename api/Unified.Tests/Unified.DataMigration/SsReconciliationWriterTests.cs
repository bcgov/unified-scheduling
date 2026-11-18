using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Unified.DataMigration.Services;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.DataMigration;
using Unified.Db.Models.UserManagement;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.DataMigration;

public sealed class SsReconciliationWriterTests : IAsyncDisposable
{
    private readonly SqliteConnection connection = new("DataSource=:memory:;Foreign Keys=True");
    private readonly UnifiedDbContext db;
    private readonly ReadCounter reads = new();
    private readonly DataMigrationRun parent = new()
    {
        Source = "SS",
        Job = "MyTeam",
        Status = "Completed",
    };
    private readonly DataMigrationRun training;
    private readonly DataMigrationRun report;

    public SsReconciliationWriterTests()
    {
        connection.Open();
        db = new SqliteTestUnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(connection).AddInterceptors(reads).Options
        );
        db.Database.EnsureCreated();
        training = new()
        {
            Source = "SS",
            Job = "Training",
            Status = "Completed",
            MyTeamRunId = parent.Id,
        };
        report = new()
        {
            Source = "SS",
            Job = "Report",
            Status = "Running",
            MyTeamRunId = parent.Id,
        };
        db.AddRange(
            parent,
            training,
            new DataMigrationRun
            {
                Source = "SS",
                Job = "Scheduling",
                Status = "Completed",
                MyTeamRunId = parent.Id,
            }
        );
        db.SaveChanges();
    }

    private static Dictionary<string, List<SsRow>> Rows() =>
        new[]
        {
            "Region",
            "Location",
            "Role",
            "User",
            "UserRole",
            "SheriffActingRank",
            "SheriffAwayLocation",
            "SheriffLeave",
            "SheriffTraining",
            "SheriffStandardTraining",
            "LookupSortOrder",
            "Assignment",
            "Duty",
            "DutySlot",
            "Shift",
            "LookupCode",
        }.ToDictionary(x => x, _ => new List<SsRow>());

    private void Outcome(string type, string id, string outcome, Guid? run = null) =>
        db.DataMigrationOutcomes.Add(
            new()
            {
                RunId = run ?? parent.Id,
                EntityType = type,
                LegacyId = id,
                Outcome = outcome,
                Reason = "OriginalReason",
            }
        );

    private void Link(string type, string id, string targetType, string key, DateTimeOffset? updated = null) =>
        db.DataMigrationRecords.Add(
            new()
            {
                Source = "SS",
                EntityType = type,
                LegacyId = id,
                TargetType = targetType,
                TargetKey = key,
                PayloadHash = "synthetic",
                SourceUpdatedAtUtc = updated,
            }
        );

    [Fact]
    public async Task Write_PreservesCoverageVerdictsTimestampPrecedenceAndAllKeyTypes()
    {
        var ct = TestContext.Current.CancellationToken;
        var rows = Rows();
        var user = new User { Id = Guid.NewGuid(), IdirName = "synthetic" };
        var permission = new Permission { Id = "synthetic" };
        db.AddRange(new Region { Id = 1, Name = "Synthetic" }, user, permission);
        string[] unchanged = ["Excluded", "DroppedBadData", "Rejected", "Failed", "NeedsAction"];
        string[] ids =
        [
            "missing",
            "duplicate",
            "unknown",
            "no-link",
            "no-target",
            "unknown-target",
            "int",
            "guid",
            "string",
            .. unchanged,
        ];
        rows["Region"].AddRange(ids.Select(id => new SsRow(new Dictionary<string, object?> { ["Id"] = id })));
        foreach (var id in ids.Where(x => x != "missing"))
            Outcome(
                "Region",
                id,
                unchanged.Contains(id) ? id
                    : id == "unknown" ? "Unexpected"
                    : "Migrated"
            );
        Outcome("Region", "duplicate", "Migrated", training.Id);
        Link("Region", "no-target", "Region", "999");
        Link("Region", "unknown-target", "NotAnEntity", "1");
        Link("Region", "int", "Region", "1");
        Link("Region", "guid", "User", user.Id.ToString("B")); // Compare parsed keys, not formatting.
        Link("Region", "string", "Permission", permission.Id);
        foreach (
            var (id, updated, created, linkDate, reason) in new[]
            {
                (
                    "changed",
                    (DateTime?)new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                    (DateTime?)null,
                    (DateTimeOffset?)null,
                    "SourceChangedSinceApplied"
                ),
                (
                    "created-fallback",
                    (DateTime?)null,
                    (DateTime?)new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    (DateTimeOffset?)new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    "OriginalReason"
                ),
                ("null-dates", (DateTime?)null, (DateTime?)null, (DateTimeOffset?)null, "OriginalReason"),
            }
        )
        {
            rows["User"]
                .Add(
                    new(
                        new Dictionary<string, object?>
                        {
                            ["Id"] = id,
                            ["UpdatedOn"] = updated,
                            ["CreatedOn"] = created,
                        }
                    )
                );
            Outcome("User", id, "Merged");
            // Stale source takes precedence even over an invalid target key.
            Link("User", id, "Region", reason == "SourceChangedSinceApplied" ? "invalid" : "1", linkDate);
        }
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        var session = new SsWriteSession(db, report);

        await new SsReconciliationWriter(db).WriteAsync(rows, session, ct);

        Assert.Equal(ids.Length + 3, session.Outcomes.Count);
        void Check(string id, string verdict, string? reason) =>
            Assert.Contains(session.Outcomes, x => x.LegacyId == id && x.Outcome == verdict && x.Reason == reason);
        Check("missing", "Failed", "MissingSourceCoverage");
        Check("duplicate", "Failed", "DuplicateSourceCoverage");
        Check("unknown", "Failed", "UnknownSourceOutcome");
        Check("no-link", "Failed", "MissingPrimaryCrosswalk");
        Check("no-target", "Failed", "MissingCrosswalkTarget");
        Check("unknown-target", "Failed", "MissingCrosswalkTarget");
        foreach (var id in new[] { "int", "guid", "string" })
            Check(id, "Migrated", "OriginalReason");
        foreach (var verdict in unchanged)
            Check(verdict, verdict, "OriginalReason");
        Check("changed", "NeedsAction", "SourceChangedSinceApplied");
        Check("created-fallback", "Merged", "OriginalReason");
        Check("null-dates", "Merged", "OriginalReason");
    }

    [Fact]
    public async Task Write_98000Rows_UsesBoundedBatchedReadsNotPerRowQueries()
    {
        var ct = TestContext.Current.CancellationToken;
        const int count = 98000;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            WITH RECURSIVE n(x) AS (SELECT 1 UNION ALL SELECT x+1 FROM n WHERE x < {count})
            INSERT INTO "Regions" ("Id","Name","CreatedOn")
            SELECT x,'Synthetic','2026-01-01T00:00:00.0000000+00:00' FROM n;
            INSERT INTO "DataMigrationOutcomes" ("RunId","EntityType","LegacyId","Outcome")
            SELECT {parent.Id},'Region',CAST("Id" AS TEXT),'MigratedWithDifference' FROM "Regions";
            INSERT INTO "DataMigrationRecords" ("Source","EntityType","LegacyId","TargetType","TargetKey","PayloadHash","IsDeleted")
            SELECT 'SS','Region',CAST("Id" AS TEXT),'Region',CAST("Id" AS TEXT),'synthetic',0 FROM "Regions";
            """,
            ct
        );
        var rows = Rows();
        rows["Region"]
            .AddRange(
                Enumerable.Range(1, count).Select(id => new SsRow(new Dictionary<string, object?> { ["Id"] = id }))
            );
        var session = new SsWriteSession(db, report);
        reads.Count = 0;

        await new SsReconciliationWriter(db).WriteAsync(rows, session, ct);

        Assert.Equal(count, session.Outcomes.Count);
        Assert.All(session.Outcomes, x => Assert.Equal("MigratedWithDifference", x.Outcome));
        Assert.Equal(2 + 2 * (count / 1000), reads.Count);
        Assert.Empty(db.ChangeTracker.Entries<Region>()); // Existence checks do not materialize target entities.
    }

    private sealed class ReadCounter : DbCommandInterceptor
    {
        public int Count;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
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
