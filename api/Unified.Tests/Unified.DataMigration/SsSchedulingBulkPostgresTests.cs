using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Audit.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Unified.Audit;
using Unified.Audit.Options;
using Unified.Authorization.Claims;
using Unified.Authorization.Seeders;
using Unified.Calendar.Seeders;
using Unified.Common.Seeding;
using Unified.Core.Seeders;
using Unified.DataMigration.FeatureFlags;
using Unified.DataMigration.Models;
using Unified.DataMigration.Options;
using Unified.DataMigration.Services;
using Unified.DataMigration.Services.Bulk;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Db.Models.UserManagement;
using Unified.Leave.Seeders;
using Unified.LoanOut.Seeders;
using Unified.Scheduling.Interceptors;
using Unified.Scheduling.Seeders;
using Unified.Stats.Seeders;
using Unified.Tests.TestHelpers;
using Unified.UserManagement;
using Unified.UserManagement.Seeders;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Unified.Tests.DataMigration;

// Scheduling PersistenceMode=Bulk proof against an isolated disposable PostgreSQL instance only.
// Frozen synthetic SS sources; identical target clones (CREATE DATABASE ... TEMPLATE) of one migrated,
// seeded target with a genuinely completed MyTeam run: PerRow on one clone, Bulk on another, compared
// after normalizing generated IDs through crosswalks (or structural owners for receipt-less children).
// Shares the Training bulk collection: Audit.NET configuration is process-wide.
[Collection(nameof(SsTrainingBulkPostgresCollection))]
public sealed class SsSchedulingBulkPostgresTests : IAsyncLifetime
{
    private const string Variable = "UNIFIED_MIGRATION_ISOLATED_TEST_CONNECTION_STRING";
    private const string SkipReason = "Set UNIFIED_MIGRATION_ISOLATED_TEST_CONNECTION_STRING for isolated PostgreSQL.";

    // Comma-separated database names on the isolated server holding frozen synthetic SS sources
    // (for example pg_dump restores of the retained ready/series/adverse rehearsal sources).
    private const string TemplatesVariable = "UNIFIED_MIGRATION_SCHEDULING_SOURCE_TEMPLATES";
    private const string BenchmarkVariable = "UNIFIED_MIGRATION_SCHEDULING_BENCHMARK_TEMPLATE";
    private const string TemplatesSkipReason =
        "Set " + TemplatesVariable + " to frozen synthetic SS source databases on the isolated server.";
    private static string? Admin => Environment.GetEnvironmentVariable(Variable);
    public static bool IsConfigured => !string.IsNullOrWhiteSpace(Admin);
    public static bool AreTemplatesConfigured =>
        IsConfigured && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(TemplatesVariable));
    public static bool IsBenchmarkConfigured =>
        IsConfigured && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(BenchmarkVariable));

    internal static readonly Type[] BusinessTypes =
    [
        typeof(CourtRoom),
        typeof(AssignmentDefinition),
        typeof(EventSeries),
        typeof(Event),
        typeof(AssignmentSeries),
        typeof(ShiftEntry),
        typeof(ShiftEntryUser),
        typeof(AssignmentEntry),
        typeof(ShiftAssignmentEntry),
        typeof(ShiftAssignmentEntryUser),
    ];

    private static readonly string[] BusinessSections = [.. BusinessTypes.Select(x => x.Name), "records"];

    private readonly string suffix = Guid.NewGuid().ToString("N")[..12];
    private readonly List<string> databases = [];
    private readonly Guid actor = Guid.NewGuid();
    private readonly IOptions<DataMigrationFeatureFlags> flags = MsOptions.Create(
        new DataMigrationFeatureFlags { Enabled = true }
    );
    private IConfiguration configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["JCInterface:ExpireRegions"] = "false",
                ["JCInterface:ExpireLocations"] = "false",
                ["JCInterface:ExpireCourtRooms"] = "false",
            }
        )
        .Build();

    // PerRow graph replacement needs the inverse journal; without it, replacements are rejected before any delete.
    private void EnableRollbackJournal() =>
        configuration = new ConfigurationBuilder()
            .AddConfiguration(configuration)
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DataMigrationRollback:EncryptionKey"] = Convert.ToBase64String(new byte[32]),
                    ["DataMigrationRollback:KeyId"] = "6c3f0f8e-2b1a-4d5e-9f00-000000000a12",
                }
            )
            .Build();

    public ValueTask InitializeAsync()
    {
        if (!IsConfigured)
            return ValueTask.CompletedTask;
        var admin = new NpgsqlConnectionStringBuilder(Admin);
        if (
            admin.Host != "127.0.0.1"
            || !admin.Database!.StartsWith("unified_migration_isolated_", StringComparison.Ordinal)
        )
            throw new InvalidOperationException("An isolated loopback PostgreSQL instance is required.");
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        global::Audit.Core.Configuration.AuditDisabled = true;
        if (databases.Count == 0)
            return;
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(Admin) { Pooling = false }.ConnectionString
        );
        await connection.OpenAsync(CancellationToken.None);
        foreach (var name in Enumerable.Reverse(databases))
        {
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    // ------------------------------------------------------------------ parity on retained synthetic sources

    [Fact(Skip = TemplatesSkipReason, SkipUnless = nameof(AreTemplatesConfigured))]
    public async Task PerRowAndBulk_ProduceEquivalentSchedulingState_OnRetainedSyntheticSources()
    {
        var ct = TestContext.Current.CancellationToken;
        EnableAudit();
        var output = TestContext.Current.TestOutputHelper;
        foreach (
            var template in Environment
                .GetEnvironmentVariable(TemplatesVariable)!
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        )
        {
            var fixture = await FixtureAsync(template, null, ct);
            var perRow = await CloneAsync(fixture, "a", MigrationPersistenceMode.PerRow, ct);
            var bulk = await CloneAsync(fixture, "b", MigrationPersistenceMode.Bulk, ct, chunk: 37);
            var watch = Stopwatch.StartNew();
            var perRowRun = await RunAsync(perRow, ct);
            var perRowElapsed = watch.Elapsed;
            watch.Restart();
            var bulkRun = await RunAsync(bulk, ct);
            var bulkElapsed = watch.Elapsed;
            await AssertNoTechnicalFailureAsync(perRow, perRowRun, ct);
            await AssertNoTechnicalFailureAsync(bulk, bulkRun, ct);
            await AssertNotRefusedAsync(bulk, bulkRun, ct);
            await AssertBulkRunSummaryOnlyAsync(bulk, bulkRun, ct);
            var expected = await SnapshotAsync(perRow, false, ct);
            AssertEquivalent(expected, await SnapshotAsync(bulk, false, ct), expected.Keys, $"{template} round 1");
            output?.WriteLine(
                $"{template}: PerRow {perRowElapsed.TotalSeconds:F1} s, Bulk {bulkElapsed.TotalSeconds:F1} s; "
                    + string.Join(", ", expected.Select(x => $"{x.Key}={x.Value.Count}"))
            );
            await AssertNoDuplicatesOrOrphansAsync(bulk, ct);

            // Rerun: no new business rows, identical evidence in both modes.
            var perRowRerun = await RunAsync(perRow, ct);
            var bulkRerun = await RunAsync(bulk, ct);
            await AssertNotRefusedAsync(bulk, bulkRerun, ct);
            await AssertRerunAsync(bulk, bulkRerun, ct);
            await AssertRerunAsync(perRow, perRowRerun, ct);
            expected = await SnapshotAsync(perRow, false, ct);
            AssertEquivalent(expected, await SnapshotAsync(bulk, false, ct), expected.Keys, $"{template} rerun");
        }
    }

    // ------------------------------------------------------------------ deterministic adverse cases

    // Explicit Scheduling edge cases on top of the retained "ready" synthetic source. IDs 910000+ avoid the
    // template's 900000 range. Expected rule outcomes are whatever PerRow decides; the proof is that Bulk
    // reproduces them exactly (asserted per case below where the rule is unambiguous).
    private const string AdverseSourceDelta = """
        INSERT INTO "User" ("Id","CreatedOn","HomeLocationId","FirstName","LastName","Email","Discriminator","Rank",
            "IdirName","IsEnabled","Excused")
        VALUES ('33333333-3333-4333-8333-333333333333','2020-01-01Z',900000,'Synthetic','Second',
            'ss-adverse-second@example.invalid','Sheriff','Deputy Sheriff','ss-adverse-second',true,false);
        INSERT INTO "Shift" ("Id","StartDate","EndDate","LocationId","CreatedOn","SheriffId","Timezone","OvertimeHours","Comment")
        VALUES
          -- touching (08-12 / 12-14), nested inside the second (12:30-13:30), then a gap (15-17)
          (910000,'2026-11-03T08:00Z','2026-11-03T12:00Z',900000,'2020-01-01Z','11111111-1111-4111-8111-111111111111','Etc/UTC',0,'adverse touching A'),
          (910001,'2026-11-03T12:00Z','2026-11-03T14:00Z',900000,'2020-01-01Z','11111111-1111-4111-8111-111111111111','Etc/UTC',0,'adverse touching B'),
          (910002,'2026-11-03T12:30Z','2026-11-03T13:30Z',900000,'2020-01-01Z','11111111-1111-4111-8111-111111111111','Etc/UTC',0,'adverse nested'),
          (910003,'2026-11-03T15:00Z','2026-11-03T17:00Z',900000,'2020-01-01Z','11111111-1111-4111-8111-111111111111','Etc/UTC',0,'adverse gap'),
          -- second user overlapping the same day (never merged across users)
          (910010,'2026-11-03T08:00Z','2026-11-03T12:00Z',900000,'2020-01-01Z','33333333-3333-4333-8333-333333333333','Etc/UTC',1.5,'adverse other user'),
          -- invalid (reversed) interval
          (910004,'2026-11-04T16:00Z','2026-11-04T08:00Z',900000,'2020-01-01Z','11111111-1111-4111-8111-111111111111','Etc/UTC',0,'adverse reversed'),
          -- DST fall-back night in America/Vancouver (01:00 PDT -> 04:00 PST)
          (910005,'2026-11-01T08:00Z','2026-11-01T12:00Z',900000,'2020-01-01Z','11111111-1111-4111-8111-111111111111','America/Vancouver',0,'adverse dst'),
          -- future (after cutover) shifts, with a gap between two halves of one day
          (910007,'2026-12-10T08:00Z','2026-12-10T16:00Z',900000,'2020-01-01Z','11111111-1111-4111-8111-111111111111','Etc/UTC',0,'adverse future'),
          (910008,'2026-12-11T08:00Z','2026-12-11T12:00Z',900000,'2020-01-01Z','33333333-3333-4333-8333-333333333333','Etc/UTC',0,'adverse future gap A'),
          (910009,'2026-12-11T14:00Z','2026-12-11T18:00Z',900000,'2020-01-01Z','33333333-3333-4333-8333-333333333333','Etc/UTC',0,'adverse future gap B'),
          -- standalone shifts later edited in the target (drift) and changed in the source (round 2)
          (910030,'2026-11-09T08:00Z','2026-11-09T16:00Z',900000,'2020-01-01Z','33333333-3333-4333-8333-333333333333','Etc/UTC',0,'adverse drift target'),
          (910031,'2026-11-10T08:00Z','2026-11-10T16:00Z',900000,'2020-01-01Z','33333333-3333-4333-8333-333333333333','Etc/UTC',0,'adverse owned update');
        INSERT INTO "Duty" ("Id","StartDate","EndDate","LocationId","AssignmentId","Timezone","CreatedOn","Comment")
        VALUES
          (910000,'2026-11-03T08:00Z','2026-11-03T16:00Z',900000,900000,'Etc/UTC','2020-01-01Z','adverse duty'),
          (910001,'2026-11-06T16:00Z','2026-11-06T08:00Z',900000,900000,'Etc/UTC','2020-01-01Z','adverse reversed duty'),
          (910002,'2026-12-10T08:00Z','2026-12-10T16:00Z',900000,900010,'Etc/UTC','2020-01-01Z','adverse future duty'),
          (910003,'2026-11-01T08:00Z','2026-11-01T12:00Z',900000,900000,'America/Vancouver','2020-01-01Z','adverse dst duty'),
          (910004,'2026-11-07T08:00Z','2026-11-07T16:00Z',900000,NULL,'Etc/UTC','2020-01-01Z','adverse duty without assignment');
        INSERT INTO "DutySlot" ("Id","StartDate","EndDate","DutyId","SheriffId","Timezone","CreatedOn","LocationId",
            "IsNotAvailable","IsNotRequired","IsOvertime","IsClosed")
        VALUES
          (910000,'2026-11-03T08:00Z','2026-11-03T12:00Z',910000,'11111111-1111-4111-8111-111111111111','Etc/UTC','2020-01-01Z',900000,false,false,false,false),
          (910001,'2026-11-03T12:00Z','2026-11-03T16:00Z',910000,NULL,'Etc/UTC','2020-01-01Z',900000,false,false,false,false),
          (910007,'2026-11-03T08:00Z','2026-11-03T12:00Z',910000,'33333333-3333-4333-8333-333333333333','Etc/UTC','2020-01-01Z',900000,false,false,true,false),
          (910002,'2026-11-06T16:00Z','2026-11-06T08:00Z',910001,'11111111-1111-4111-8111-111111111111','Etc/UTC','2020-01-01Z',900000,false,false,false,false),
          (910003,'2026-11-06T08:00Z','2026-11-06T10:00Z',910001,'33333333-3333-4333-8333-333333333333','Etc/UTC','2020-01-01Z',900000,false,false,false,false),
          (910004,'2026-12-10T09:00Z','2026-12-10T11:00Z',910002,'11111111-1111-4111-8111-111111111111','Etc/UTC','2020-01-01Z',900000,false,false,false,false),
          (910005,'2026-12-10T12:00Z','2026-12-10T14:00Z',910002,'33333333-3333-4333-8333-333333333333','Etc/UTC','2020-01-01Z',900000,false,false,false,false),
          (910009,'2026-12-10T14:00Z','2026-12-10T16:00Z',910002,NULL,'Etc/UTC','2020-01-01Z',900000,false,false,false,false),
          (910006,'2026-11-01T08:00Z','2026-11-01T12:00Z',910003,'11111111-1111-4111-8111-111111111111','America/Vancouver','2020-01-01Z',900000,false,false,false,false),
          (910008,'2026-11-07T08:00Z','2026-11-07T16:00Z',910004,'11111111-1111-4111-8111-111111111111','Etc/UTC','2020-01-01Z',900000,false,true,false,false);
        """;

    // A pre-existing Unified court room equal to the source room: Scheduling links it (linked-only target).
    private const string AdverseTargetDelta = """
        INSERT INTO "CourtRooms" ("Room","LocationId","CreatedOn")
        SELECT 'Ready Room', "TargetKey"::int, '2020-01-01Z' FROM "DataMigrationRecords"
        WHERE "Source"='SS' AND "EntityType"='Location' AND "LegacyId"='900000';
        """;

    // Round 2 (Bulk-compatible): a change to the linked-only room's source, a Unified edit to a migrated target
    // without a source change, and new rows (a new historical graph).
    private const string AdverseRound2Delta = """
        UPDATE "LookupCode" SET "Description"='Synthetic ready room renamed', "UpdatedOn"='2026-11-20Z' WHERE "Id"=900000;
        INSERT INTO "Shift" ("Id","StartDate","EndDate","LocationId","CreatedOn","SheriffId","Timezone","OvertimeHours","Comment")
        VALUES (910040,'2026-11-16T08:00Z','2026-11-16T16:00Z',900000,'2026-11-20Z','11111111-1111-4111-8111-111111111111','Etc/UTC',0,'round 2 new');
        INSERT INTO "Duty" ("Id","StartDate","EndDate","LocationId","AssignmentId","Timezone","CreatedOn","Comment")
        VALUES (910040,'2026-11-16T08:00Z','2026-11-16T16:00Z',900000,900000,'Etc/UTC','2026-11-20Z','round 2 duty');
        INSERT INTO "DutySlot" ("Id","StartDate","EndDate","DutyId","SheriffId","Timezone","CreatedOn","LocationId",
            "IsNotAvailable","IsNotRequired","IsOvertime","IsClosed")
        VALUES (910040,'2026-11-16T08:00Z','2026-11-16T16:00Z',910040,'11111111-1111-4111-8111-111111111111','Etc/UTC','2026-11-20Z',900000,false,false,false,false);
        """;

    // Unified edit to the migrated target of Shift 910030 (applied identically to every clone).
    private const string AdverseTargetDrift = """
        UPDATE "Events" e SET "Notes"='edited in Unified', "UpdatedOn"=now()
        FROM "ShiftEntries" s JOIN "DataMigrationRecords" r ON r."TargetType"='ShiftEntry' AND r."TargetKey"=s."Id"::text
        WHERE r."Source"='SS' AND r."EntityType"='Shift' AND r."LegacyId"='910030' AND e."Id"=s."EventId";
        """;

    // Round 3 (PerRow-only paths): source changes to already-migrated rows (an UpdatedOn-only touch of a merged
    // group, a change on a target edited in Unified, a clean owned change) and a new future duty joining an
    // existing recurrence component. Bulk refuses the run explicitly; PerRow then decides each case.
    private const string AdverseRound3Delta = """
        UPDATE "Shift" SET "UpdatedOn"='2026-11-21Z' WHERE "Id"=900002;
        UPDATE "Shift" SET "Comment"='adverse drift changed', "UpdatedOn"='2026-11-21Z' WHERE "Id"=910030;
        UPDATE "Shift" SET "Comment"='adverse owned update changed', "UpdatedOn"='2026-11-21Z' WHERE "Id"=910031;
        INSERT INTO "Duty" ("Id","StartDate","EndDate","LocationId","AssignmentId","Timezone","CreatedOn","Comment")
        VALUES (910050,'2026-12-15T08:00Z','2026-12-15T16:00Z',900000,900000,'Etc/UTC','2026-11-21Z','round 3 future duty');
        INSERT INTO "DutySlot" ("Id","StartDate","EndDate","DutyId","SheriffId","Timezone","CreatedOn","LocationId",
            "IsNotAvailable","IsNotRequired","IsOvertime","IsClosed")
        VALUES (910050,'2026-12-15T08:00Z','2026-12-15T16:00Z',910050,'11111111-1111-4111-8111-111111111111','Etc/UTC','2026-11-21Z',900000,false,false,false,false);
        """;

    private static string ReadyTemplate =>
        Environment.GetEnvironmentVariable("UNIFIED_MIGRATION_SCHEDULING_READY_TEMPLATE")
        ?? Environment
            .GetEnvironmentVariable(TemplatesVariable)!
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .First(x => x.Contains("ready", StringComparison.OrdinalIgnoreCase));

    private Task<Fixture> AdverseFixtureAsync(CancellationToken ct) =>
        FixtureAsync(ReadyTemplate, AdverseSourceDelta, ct, AdverseTargetDelta);

    [Fact(Skip = TemplatesSkipReason, SkipUnless = nameof(AreTemplatesConfigured))]
    public async Task PerRowAndBulk_MatchOnDeterministicAdverseCases_AcrossRounds()
    {
        var ct = TestContext.Current.CancellationToken;
        EnableAudit();
        var output = TestContext.Current.TestOutputHelper;
        var fixture = await AdverseFixtureAsync(ct);
        // Small PerRow batches and Bulk chunks that are not a multiple of them probe version granularity.
        var perRow = await CloneAsync(fixture, "adv_a", MigrationPersistenceMode.PerRow, ct, chunk: 7, batch: 5);
        var bulk = await CloneAsync(fixture, "adv_b", MigrationPersistenceMode.Bulk, ct, chunk: 7, batch: 5);

        // Round 1.
        var perRowRun = await RunAsync(perRow, ct);
        var bulkRun = await RunAsync(bulk, ct);
        await AssertNoTechnicalFailureAsync(perRow, perRowRun, ct);
        await AssertNoTechnicalFailureAsync(bulk, bulkRun, ct);
        await AssertNotRefusedAsync(bulk, bulkRun, ct);
        var expected = await SnapshotAsync(perRow, false, ct);
        AssertEquivalent(expected, await SnapshotAsync(bulk, false, ct), expected.Keys, "adverse round 1");
        await AssertNoDuplicatesOrOrphansAsync(bulk, ct);
        var outcomes = await OutcomesAsync(perRow, perRowRun, ct);
        foreach (
            var line in outcomes.Where(x => x.Key.Id.StartsWith("910", StringComparison.Ordinal)).OrderBy(x => x.Key)
        )
            output?.WriteLine($"round 1 {line.Key.Type}:{line.Key.Id} -> {line.Value}");
        // Rejected parent with children: the duty without an assignment is rejected and its slot is rejected as a
        // dependant; slots on a sheriff without a matching shift fail the material link rule. None own a target.
        Assert.Equal("Rejected:MissingAssignment", outcomes[("Duty", "910004")]);
        Assert.Equal("Rejected:ParentDutyNotMigrated", outcomes[("DutySlot", "910008")]);
        Assert.Equal("Failed:MaterialMissingShiftUserLink", outcomes[("DutySlot", "910002")]);
        Assert.Equal("NeedsAction:FutureGapRequiresSourceRepair", outcomes[("Shift", "910008")]);
        Assert.Equal("MigratedWithDifference:HistoricalGapBlocksSeparate", outcomes[("Shift", "910002")]);
        Assert.Equal(
            0,
            await CountAsync(
                bulk.ConnectionString,
                """
                SELECT COUNT(*) FROM "DataMigrationRecords"
                WHERE ("EntityType"='Duty' AND "LegacyId"='910004')
                   OR ("EntityType"='DutySlot' AND "LegacyId"='910008')
                """,
                ct
            )
        );
        output?.WriteLine(
            "records for Failed/NeedsAction rows: "
                + await CountAsync(
                    bulk.ConnectionString,
                    """
                    SELECT COUNT(*) FROM "DataMigrationRecords"
                    WHERE ("EntityType"='DutySlot' AND "LegacyId" IN ('910002','910003','910005'))
                       OR ("EntityType"='Shift' AND "LegacyId" IN ('910008','910009'))
                    """,
                    ct
                )
        );
        // Linked-only room: the pre-existing Unified court room was linked, not inserted.
        Assert.Equal(
            1,
            await CountAsync(
                bulk.ConnectionString,
                """
                SELECT COUNT(*) FROM "DataMigrationRecords"
                WHERE "EntityType"='CourtRoom' AND "LegacyId"='900000' AND "CreatedByRunId" IS NULL
                """,
                ct
            )
        );

        // Round 2: Unified edits one migrated target (no source change); the source adds rows and renames a room.
        await ExecuteAsync(perRow.ConnectionString, AdverseTargetDrift, ct);
        await ExecuteAsync(bulk.ConnectionString, AdverseTargetDrift, ct);
        await ExecuteAsync(fixture.Source, AdverseRound2Delta, ct);
        perRowRun = await RunAsync(perRow, ct);
        bulkRun = await RunAsync(bulk, ct);
        await AssertNoTechnicalFailureAsync(perRow, perRowRun, ct);
        await AssertNoTechnicalFailureAsync(bulk, bulkRun, ct);
        await AssertNotRefusedAsync(bulk, bulkRun, ct);
        expected = await SnapshotAsync(perRow, false, ct);
        AssertEquivalent(expected, await SnapshotAsync(bulk, false, ct), expected.Keys, "adverse round 2");
        await AssertNoDuplicatesOrOrphansAsync(bulk, ct);
        outcomes = await OutcomesAsync(perRow, perRowRun, ct);
        foreach (
            var key in new[] { ("Shift", "910030"), ("CourtRoom", "900000"), ("Shift", "910040"), ("Duty", "910040") }
        )
            output?.WriteLine($"round 2 {key.Item1}:{key.Item2} -> {outcomes.GetValueOrDefault(key)}");
        Assert.Equal("Migrated:", outcomes[("Shift", "910040")]);

        // Round 3: source changes to already-migrated rows are PerRow-only paths. Bulk refuses the whole run before
        // any business write; the operator then runs PerRow, which converges with the PerRow clone.
        await ExecuteAsync(fixture.Source, AdverseRound3Delta, ct);
        var beforeRefusal = await SnapshotAsync(bulk, true, ct);
        var refused = await RunAsync(bulk, ct);
        await using (var context = bulk.Db())
        {
            var run = await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == refused, ct);
            Assert.Equal("Failed", run.Status);
            Assert.Equal(0, run.InsertedCount);
            Assert.Equal(0, run.UpdatedCount);
            var refusal = await context
                .DataMigrationOutcomes.AsNoTracking()
                .SingleAsync(x => x.RunId == refused && x.EntityType == "SchedulingBulk", ct);
            output?.WriteLine("round 3 Bulk refusal: " + refusal.Reason);
            Assert.StartsWith("BulkRequiresPerRow:", refusal.Reason);
        }
        AssertEquivalent(beforeRefusal, await SnapshotAsync(bulk, true, ct), BusinessSections, "bulk refusal");
        perRowRun = await RunAsync(perRow, ct);
        await AssertNoTechnicalFailureAsync(perRow, perRowRun, ct);
        bulk.Options.Sources.SS.Jobs.Scheduling.PersistenceMode = MigrationPersistenceMode.PerRow;
        var resolved = await RunAsync(bulk, ct);
        await AssertNoTechnicalFailureAsync(bulk, resolved, ct);
        expected = await SnapshotAsync(perRow, true, ct);
        AssertEquivalent(
            expected,
            await SnapshotAsync(bulk, true, ct),
            [.. BusinessSections, "linked", "audit"],
            "adverse round 3 (PerRow after Bulk refusal)"
        );
        var perRowRound3 = await OutcomesAsync(perRow, perRowRun, ct);
        var bulkRound3 = await OutcomesAsync(bulk, resolved, ct);
        foreach (var key in new[] { ("Shift", "900002"), ("Shift", "910030"), ("Shift", "910031"), ("Duty", "910050") })
        {
            output?.WriteLine($"round 3 PerRow {key.Item1}:{key.Item2} -> {perRowRound3.GetValueOrDefault(key)}");
            Assert.Equal(perRowRound3.GetValueOrDefault(key), bulkRound3.GetValueOrDefault(key));
        }
    }

    // ------------------------------------------------------------------ mixed-mode resume (required)

    [Fact(Skip = TemplatesSkipReason, SkipUnless = nameof(AreTemplatesConfigured))]
    public async Task MixedMode_PerRowCanceledThenBulk_EqualsFullPerRow_AndReverse()
    {
        var ct = TestContext.Current.CancellationToken;
        EnableAudit();
        var output = TestContext.Current.TestOutputHelper;
        var fixture = await AdverseFixtureAsync(ct);
        var reference = await CloneAsync(fixture, "mx_ref", MigrationPersistenceMode.PerRow, ct, chunk: 3, batch: 3);
        var referenceRun = await RunAsync(reference, ct);
        await AssertNoTechnicalFailureAsync(reference, referenceRun, ct);
        var full = await SnapshotAsync(reference, true, ct);
        var referenceRecords = await CountAsync(
            reference.ConnectionString,
            "SELECT COUNT(*) FROM \"DataMigrationRecords\" WHERE \"EntityType\" IN ('Shift','Duty','DutySlot')",
            ct
        );

        // PerRow canceled part-way (token cancel after a fixed number of committed transactions).
        var mixed = await CloneAsync(fixture, "mx_m", MigrationPersistenceMode.PerRow, ct, chunk: 3, batch: 3);
        var canceled = await CancelPerRowAsync(mixed, referenceRecords / 2, ct);
        var partialRecords = await CountAsync(
            mixed.ConnectionString,
            "SELECT COUNT(*) FROM \"DataMigrationRecords\" WHERE \"EntityType\" IN ('Shift','Duty','DutySlot')",
            ct
        );
        output?.WriteLine($"PerRow canceled after partial commit: {partialRecords}/{referenceRecords} graph records");
        Assert.InRange(partialRecords, 1, referenceRecords - 1);
        // (3) No half graph: every committed target's crosswalk member set equals the full run's member set.
        await AssertWholeGraphsAsync(mixed, reference, ct);
        await AssertNoDuplicatesOrOrphansAsync(mixed, ct);

        // Identical copy of the canceled state for the PerRow-resume comparison.
        var perRowResume = await CopyAsync(mixed, "mx_p", MigrationPersistenceMode.PerRow, ct);

        // (5) Lineage/acquisition accept Bulk after the Canceled PerRow run of the same job.
        mixed.Options.Sources.SS.Jobs.Scheduling.PersistenceMode = MigrationPersistenceMode.Bulk;
        var bulkRun = await RunAsync(mixed, ct);
        await AssertNoTechnicalFailureAsync(mixed, bulkRun, ct);
        await AssertNotRefusedAsync(mixed, bulkRun, ct);
        var perRowRun = await RunAsync(perRowResume, ct);
        await AssertNoTechnicalFailureAsync(perRowResume, perRowRun, ct);

        // (1) No duplicate targets or crosswalks.
        await AssertNoDuplicatesOrOrphansAsync(mixed, ct);
        // (4) Bulk resume classifies PerRow-committed rows exactly like a PerRow resume: identical runs
        // (counters), outcomes, work items, checkpoints, records, mutations, audit and business rows.
        var expectedResume = await SnapshotAsync(perRowResume, false, ct);
        AssertEquivalent(
            expectedResume,
            await SnapshotAsync(mixed, false, ct),
            expectedResume.Keys,
            "PerRow-canceled resume"
        );
        // (2) Final target equals a full PerRow run (IDs normalized through crosswalks).
        AssertEquivalent(
            full,
            await SnapshotAsync(mixed, true, ct),
            [.. BusinessSections, "linked", "audit"],
            "mixed vs full PerRow"
        );
        await using (var context = mixed.Db())
        {
            var first = await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == canceled, ct);
            var second = await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == bulkRun, ct);
            output?.WriteLine(
                $"Bulk resume counters: scanned={second.ScannedCount} inserted={second.InsertedCount} "
                    + $"updated={second.UpdatedCount} unchanged={second.UnchangedCount} skipped={second.SkippedCount} "
                    + $"rejected={second.RejectedCount} failed={second.FailedCount}; canceled PerRow committed "
                    + $"{first.CommittedBatchCount} batches"
            );
            Assert.Equal(first.MyTeamRunId, second.MyTeamRunId);
        }
        // Rerun after mixed mode: nothing new.
        var rerun = await RunAsync(mixed, ct);
        await AssertRerunAsync(mixed, rerun, ct);

        // Undo of the Bulk run that resumed a PerRow run: either refused (it touched PerRow-owned graphs) or
        // exactly restores the canceled PerRow state; never anything in between.
        await using (var context = mixed.Db())
        {
            var undo = await new SsBulkUndo(context).UndoAsync(bulkRun, ct);
            output?.WriteLine(
                "Undo of Bulk resume: " + (undo.ReceiptRunId is null ? string.Join(",", undo.Blockers) : "applied")
            );
            Assert.Contains("LaterSameJobRunMustRollbackFirst", undo.Blockers);
        }

        // Reverse: Bulk crashes inside chunk 3 (chunks 1-2 committed), then PerRow resumes; and an identical
        // copy resumes in Bulk. Both equal the full PerRow run and each other.
        var reverse = await CloneAsync(fixture, "mx_r", MigrationPersistenceMode.Bulk, ct, chunk: 3, batch: 3);
        await RunAsync(
            reverse,
            ct,
            hook: (index, _) =>
                index == 2
                    ? throw new InvalidOperationException("Synthetic crash inside chunk 3.")
                    : Task.CompletedTask,
            expectFailure: true
        );
        await AssertWholeGraphsAsync(reverse, reference, ct);
        await AssertNoDuplicatesOrOrphansAsync(reverse, ct);
        var bulkResume = await CopyAsync(reverse, "mx_rb", MigrationPersistenceMode.Bulk, ct);
        reverse.Options.Sources.SS.Jobs.Scheduling.PersistenceMode = MigrationPersistenceMode.PerRow;
        var reversePerRow = await RunAsync(reverse, ct);
        await AssertNoTechnicalFailureAsync(reverse, reversePerRow, ct);
        var reverseBulk = await RunAsync(bulkResume, ct);
        await AssertNoTechnicalFailureAsync(bulkResume, reverseBulk, ct);
        await AssertNotRefusedAsync(bulkResume, reverseBulk, ct);
        await AssertNoDuplicatesOrOrphansAsync(reverse, ct);
        await AssertNoDuplicatesOrOrphansAsync(bulkResume, ct);
        var reverseExpected = await SnapshotAsync(reverse, false, ct);
        AssertEquivalent(
            reverseExpected,
            await SnapshotAsync(bulkResume, false, ct),
            reverseExpected.Keys,
            "Bulk-crash resume"
        );
        AssertEquivalent(
            full,
            await SnapshotAsync(reverse, true, ct),
            [.. BusinessSections, "linked", "audit"],
            "reverse vs full PerRow"
        );
    }

    // ------------------------------------------------------------------ safety

    [Fact(Skip = TemplatesSkipReason, SkipUnless = nameof(AreTemplatesConfigured))]
    public async Task Bulk_RefusesConcurrentAndTerminalRuns_ResumesAfterCrashAbortAndCancelWithoutDuplicates()
    {
        var ct = TestContext.Current.CancellationToken;
        EnableAudit();
        var fixture = await AdverseFixtureAsync(ct);
        var reference = await CloneAsync(fixture, "sf_ref", MigrationPersistenceMode.PerRow, ct, chunk: 3, batch: 3);
        var referenceRun = await RunAsync(reference, ct);
        await AssertNoTechnicalFailureAsync(reference, referenceRun, ct);
        var expected = await SnapshotAsync(reference, true, ct);
        string[] compared = [.. BusinessSections, "linked", "audit"];
        await using (var context = reference.Db())
        {
            var refused = await new SsBulkUndo(context).UndoAsync(referenceRun, ct);
            Assert.Null(refused.ReceiptRunId);
            Assert.Contains("RunNotBulkMode", refused.Blockers);
        }

        // Crash inside chunk 3 after chunks 1-2 committed; a duplicate execution, a second trigger and an
        // undo are all refused while the run is live (undo without waiting on chunk locks).
        var crash = await CloneAsync(fixture, "sf_crash", MigrationPersistenceMode.Bulk, ct, chunk: 3, batch: 3);
        await using (var context = crash.Db())
        {
            var first = await Operations(context, crash.Options).TriggerAsync("SS", "Scheduling", false, actor, ct);
            await using var second = crash.Db();
            var busy = await Assert.ThrowsAsync<MigrationOperationException>(() =>
                Operations(second, crash.Options).TriggerAsync("SS", "Scheduling", false, actor, ct)
            );
            Assert.Equal("JobBusyPausedOrLineageNotReady", busy.Code);
            await using var worker = crash.Db();
            var probed = false;
            var writer = new SsSchedulingBulkWriter(worker, MsOptions.Create(AuditOptions))
            {
                BeforeChunkCommitAsync = async (index, token) =>
                {
                    if (index == 1)
                    {
                        await using var duplicate = crash.Db();
                        var owner = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                            Executor(duplicate, crash.Options).ExecuteAcquiredAsync(first.RunId, token)
                        );
                        Assert.Contains("execution owner", owner.Message);
                        await using var undoContext = crash.Db();
                        var undo = await new SsBulkUndo(undoContext)
                            .UndoAsync(first.RunId, token)
                            .WaitAsync(TimeSpan.FromSeconds(30), token);
                        Assert.Equal(["SourceJobRunning"], undo.Blockers);
                        probed = true;
                    }
                    if (index == 2)
                        throw new InvalidOperationException("Synthetic crash inside the chunk transaction.");
                },
            };
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Executor(worker, crash.Options, writer).ExecuteAcquiredAsync(first.RunId, ct)
            );
            Assert.True(probed);
            await using var check = crash.Db();
            var failed = await check.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == first.RunId, ct);
            Assert.Equal("Failed", failed.Status);
            Assert.Equal(2, failed.CommittedBatchCount);
            Assert.Equal(
                "ExecutionFailed",
                (
                    await check.DataMigrationFailures.AsNoTracking().SingleAsync(x => x.RunId == first.RunId, ct)
                ).ErrorType
            );
            await AssertNoDuplicatesOrOrphansAsync(crash, ct);
            await AssertWholeGraphsAsync(crash, reference, ct);
            Assert.True(
                await check.DataMigrationRecords.AsNoTracking().CountAsync(x => x.CreatedByRunId == first.RunId, ct) > 0
            );
            // Terminal run IDs are refused without changing evidence.
            await using var terminal = crash.Db();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Executor(terminal, crash.Options).ExecuteAcquiredAsync(first.RunId, ct)
            );
            await using var after = crash.Db();
            Assert.Equal(
                1,
                await after.DataMigrationFailures.AsNoTracking().CountAsync(x => x.RunId == first.RunId, ct)
            );
        }
        var resumed = await RunAsync(crash, ct);
        await AssertNoTechnicalFailureAsync(crash, resumed, ct);
        await AssertNoDuplicatesOrOrphansAsync(crash, ct);
        AssertEquivalent(expected, await SnapshotAsync(crash, true, ct), compared, "crash resume");
        var rerun = await RunAsync(crash, ct);
        await AssertRerunAsync(crash, rerun, ct);
        await using (var context = crash.Db())
        {
            var run = await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == rerun, ct);
            await using var terminal = crash.Db();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Executor(terminal, crash.Options).ExecuteAcquiredAsync(rerun, ct)
            );
            await using var after = crash.Db();
            var unchanged = await after.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == rerun, ct);
            Assert.Equal(run.Status, unchanged.Status);
            Assert.Equal(run.CompletedOn, unchanged.CompletedOn);
            Assert.Equal(0, await after.DataMigrationFailures.AsNoTracking().CountAsync(x => x.RunId == rerun, ct));
        }
        AssertEquivalent(expected, await SnapshotAsync(crash, true, ct), compared, "crash rerun");

        // Abort control set while a chunk is in flight: that chunk commits, the next is fenced.
        var abort = await CloneAsync(fixture, "sf_abort", MigrationPersistenceMode.Bulk, ct, chunk: 3, batch: 3);
        var aborted = await RunAsync(
            abort,
            ct,
            hook: async (index, token) =>
            {
                if (index == 1)
                    await ExecuteAsync(
                        abort.ConnectionString,
                        """
                        INSERT INTO "DataMigrationControls" ("Source","IsPaused","AbortRequestedOn","ChangedOn","ChangedBy")
                        VALUES ('SS:Scheduling', false, now(), now(), 'test')
                        ON CONFLICT ("Source") DO UPDATE SET "AbortRequestedOn"=now()
                        """,
                        token
                    );
            },
            expectFailure: true
        );
        await using (var context = abort.Db())
        {
            var run = await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == aborted, ct);
            Assert.Equal("Failed", run.Status);
            Assert.Equal(2, run.CommittedBatchCount);
        }
        await AssertNoDuplicatesOrOrphansAsync(abort, ct);
        await AssertWholeGraphsAsync(abort, reference, ct);
        await ExecuteAsync(abort.ConnectionString, "DELETE FROM \"DataMigrationControls\"", ct);
        var afterAbort = await RunAsync(abort, ct);
        await AssertNoTechnicalFailureAsync(abort, afterAbort, ct);
        AssertEquivalent(expected, await SnapshotAsync(abort, true, ct), compared, "abort resume");

        // Token cancellation between chunks stops cleanly with a Canceled failure.
        var cancel = await CloneAsync(fixture, "sf_cancel", MigrationPersistenceMode.Bulk, ct, chunk: 3, batch: 3);
        using var source = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await using (var context = cancel.Db())
        {
            var accepted = await Operations(context, cancel.Options).TriggerAsync("SS", "Scheduling", false, actor, ct);
            await using var worker = cancel.Db();
            var writer = new SsSchedulingBulkWriter(worker, MsOptions.Create(AuditOptions))
            {
                BeforeChunkCommitAsync = (index, _) =>
                {
                    if (index == 1)
                        source.CancelAfter(TimeSpan.Zero);
                    return Task.CompletedTask;
                },
            };
            await Assert.ThrowsAnyAsync<Exception>(() =>
                Executor(worker, cancel.Options, writer).ExecuteAcquiredAsync(accepted.RunId, source.Token)
            );
            await using var check = cancel.Db();
            var run = await check.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == accepted.RunId, ct);
            Assert.Equal("Failed", run.Status);
            Assert.InRange(run.CommittedBatchCount, 1, 2);
            Assert.Equal(
                "Canceled",
                (
                    await check.DataMigrationFailures.AsNoTracking().SingleAsync(x => x.RunId == accepted.RunId, ct)
                ).ErrorType
            );
        }
        await AssertNoDuplicatesOrOrphansAsync(cancel, ct);
        await AssertWholeGraphsAsync(cancel, reference, ct);
        var afterCancel = await RunAsync(cancel, ct);
        await AssertNoTechnicalFailureAsync(cancel, afterCancel, ct);
        AssertEquivalent(expected, await SnapshotAsync(cancel, true, ct), compared, "cancel resume");

        // A cached rule input (a user's name, used for shift titles) changes between the preflight and the real
        // pass: refused before any business write, so a later refusal can never follow committed chunks.
        var moved = await CloneAsync(fixture, "sf_moved", MigrationPersistenceMode.Bulk, ct, chunk: 3, batch: 3);
        var beforeMoved = await SnapshotAsync(moved, true, ct);
        await using (var context = moved.Db())
        {
            var accepted = await Operations(context, moved.Options).TriggerAsync("SS", "Scheduling", false, actor, ct);
            await using var worker = moved.Db();
            var writer = new SsSchedulingBulkWriter(worker, MsOptions.Create(AuditOptions))
            {
                AfterPreflightAsync = token =>
                    ExecuteAsync(
                        moved.ConnectionString,
                        """
                        UPDATE "Users" SET "LastName" = "LastName" || 'x'
                        WHERE "Id" = (SELECT "Id" FROM "Users" WHERE "LastName" IS NOT NULL ORDER BY "Id" LIMIT 1)
                        """,
                        token
                    ),
            };
            await Executor(worker, moved.Options, writer).ExecuteAcquiredAsync(accepted.RunId, ct);
            await using var check = moved.Db();
            var run = await check.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == accepted.RunId, ct);
            Assert.Equal("Failed", run.Status);
            Assert.Equal(0, run.InsertedCount);
            Assert.Equal(
                "BulkRequiresPerRow:TargetChangedDuringBulkRun",
                (
                    await check
                        .DataMigrationOutcomes.AsNoTracking()
                        .SingleAsync(x => x.RunId == accepted.RunId && x.EntityType == "SchedulingBulk", ct)
                ).Reason
            );
        }
        AssertEquivalent(beforeMoved, await SnapshotAsync(moved, true, ct), BusinessSections, "moved preflight");
        var afterMoved = await RunAsync(moved, ct);
        await AssertNoTechnicalFailureAsync(moved, afterMoved, ct);
        await AssertNotRefusedAsync(moved, afterMoved, ct);
        await AssertNoDuplicatesOrOrphansAsync(moved, ct);

        // Lock cycle: another transaction holds ROW EXCLUSIVE on Users (as a Training compose chunk does) and, once
        // this run is waiting for its chunk locks, needs a scheduling table (as a crosswalk insert or an
        // application write does). The bulk chunk must back off instead of PostgreSQL aborting either side.
        var cycle = await CloneAsync(fixture, "sf_cycle", MigrationPersistenceMode.Bulk, ct, chunk: 3, batch: 3);
        var holding = new TaskCompletionSource();
        Task? other = null;
        await using (var context = cycle.Db())
        {
            var accepted = await Operations(context, cycle.Options).TriggerAsync("SS", "Scheduling", false, actor, ct);
            await using var worker = cycle.Db();
            var writer = new SsSchedulingBulkWriter(worker, MsOptions.Create(AuditOptions))
            {
                AfterPreflightAsync = async token =>
                {
                    other = Task.Run(
                        async () =>
                        {
                            await using var connection = new NpgsqlConnection(cycle.ConnectionString);
                            await connection.OpenAsync(ct);
                            await using var tx = await connection.BeginTransactionAsync(ct);
                            await using (
                                var update = new NpgsqlCommand(
                                    "UPDATE \"Users\" SET \"UpdatedOn\" = \"UpdatedOn\" WHERE \"Id\" = (SELECT min(\"Id\"::text)::uuid FROM \"Users\")",
                                    connection,
                                    tx
                                )
                            )
                                await update.ExecuteNonQueryAsync(ct);
                            holding.SetResult();
                            var waited = Stopwatch.StartNew();
                            while (waited.Elapsed < TimeSpan.FromSeconds(30))
                            {
                                await using var probe = new NpgsqlCommand(
                                    "SELECT count(*) FROM pg_locks WHERE NOT granted AND pid <> pg_backend_pid() AND database = (SELECT oid FROM pg_database WHERE datname = current_database())",
                                    connection,
                                    tx
                                );
                                if ((long)(await probe.ExecuteScalarAsync(ct))! > 0)
                                    break;
                                await Task.Delay(10, ct);
                            }
                            Assert.True(waited.Elapsed < TimeSpan.FromSeconds(30), "bulk chunk never waited");
                            await using (
                                var write = new NpgsqlCommand(
                                    "LOCK TABLE \"ShiftEntries\" IN ROW EXCLUSIVE MODE",
                                    connection,
                                    tx
                                )
                            )
                                await write.ExecuteNonQueryAsync(ct);
                            await tx.CommitAsync(ct);
                        },
                        ct
                    );
                    await holding.Task.WaitAsync(token);
                },
            };
            await Executor(worker, cycle.Options, writer).ExecuteAcquiredAsync(accepted.RunId, ct);
            await other!;
            await AssertNoTechnicalFailureAsync(cycle, accepted.RunId, ct);
            await AssertNotRefusedAsync(cycle, accepted.RunId, ct);
        }
        await AssertNoDuplicatesOrOrphansAsync(cycle, ct);
        AssertEquivalent(expected, await SnapshotAsync(cycle, true, ct), BusinessSections, "lock cycle");

        // A Training run in progress refuses Bulk before any business write (they fence the same tables).
        var training = await CloneAsync(fixture, "sf_training", MigrationPersistenceMode.Bulk, ct, chunk: 3, batch: 3);
        var beforeTraining = await SnapshotAsync(training, true, ct);
        await ExecuteAsync(
            training.ConnectionString,
            """
            CREATE TEMP TABLE fake AS SELECT * FROM "DataMigrationRuns" WHERE "Job"='MyTeam' LIMIT 1;
            UPDATE fake SET "Id"=gen_random_uuid(), "Job"='Training', "Status"='Running', "CompletedOn"=NULL;
            INSERT INTO "DataMigrationRuns" SELECT * FROM fake;
            """,
            ct
        );
        var refusedTraining = await RunAsync(training, ct);
        await using (var context = training.Db())
        {
            Assert.Equal(
                "BulkRequiresPerRow:TrainingRunInProgress",
                (
                    await context
                        .DataMigrationOutcomes.AsNoTracking()
                        .SingleAsync(x => x.RunId == refusedTraining && x.EntityType == "SchedulingBulk", ct)
                ).Reason
            );
        }
        AssertEquivalent(beforeTraining, await SnapshotAsync(training, true, ct), BusinessSections, "training running");
    }

    [Fact(Skip = TemplatesSkipReason, SkipUnless = nameof(AreTemplatesConfigured))]
    public async Task Bulk_DryRunPersistsNothing_ModeIsBoundAtTrigger_UndoRestoresBaselineOrRefuses()
    {
        var ct = TestContext.Current.CancellationToken;
        EnableAudit();
        var fixture = await AdverseFixtureAsync(ct);
        var clone = await CloneAsync(fixture, "undo", MigrationPersistenceMode.Bulk, ct, chunk: 3, batch: 3);
        string[] compared = [.. BusinessSections, "linked"];
        var baseline = await SnapshotAsync(clone, true, ct);

        // Bulk + DryRun uses the existing simulation path and persists no business rows. Dry lineage needs a dry
        // MyTeam, which runs on a fresh seeded target (this source's MyTeam is already applied on the fixture).
        var fresh = new Clone(
            await CreateDatabaseAsync(
                $"migration_it_sched_dry_{databases.Count}_{suffix}",
                await SeededTargetAsync(ct),
                ct
            ),
            NewOptions(fixture.Source, MigrationPersistenceMode.Bulk, 3, 3),
            fixture.BaselineMax
        );
        var freshBaseline = await SnapshotAsync(fresh, true, ct);
        var dryMyTeam = await RunAsync(fresh, ct, job: "MyTeam", dryRun: true);
        await AssertRunAsync(fresh, dryMyTeam, "Completed", ct);
        var dryScheduling = await RunAsync(fresh, ct, dryRun: true);
        await using (var context = fresh.Db())
        {
            var run = await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == dryScheduling, ct);
            Assert.True(run.DryRun);
            Assert.True(run.ScannedCount > 0);
        }
        AssertEquivalent(freshBaseline, await SnapshotAsync(fresh, true, ct), compared, "bulk dry run");

        // The persistence mode is bound into the audited trigger; a later configuration change refuses the run.
        await using (var context = clone.Db())
        {
            var accepted = await Operations(context, clone.Options).TriggerAsync("SS", "Scheduling", false, actor, ct);
            clone.Options.Sources.SS.Jobs.Scheduling.PersistenceMode = MigrationPersistenceMode.PerRow;
            await using var worker = clone.Db();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Executor(worker, clone.Options).ExecuteAcquiredAsync(accepted.RunId, ct)
            );
            clone.Options.Sources.SS.Jobs.Scheduling.PersistenceMode = MigrationPersistenceMode.Bulk;
            await using var check = clone.Db();
            var run = await check.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == accepted.RunId, ct);
            Assert.Equal("Failed", run.Status);
            Assert.Equal(0, run.InsertedCount);
        }
        AssertEquivalent(baseline, await SnapshotAsync(clone, true, ct), compared, "mode change refused");

        var applied = await RunAsync(clone, ct);
        await AssertNoTechnicalFailureAsync(clone, applied, ct);
        var appliedState = await SnapshotAsync(clone, true, ct);
        Assert.True(await CountAsync(clone.ConnectionString, "SELECT COUNT(*) FROM \"ShiftEntries\"", ct) > 0);

        // Crosswalk-scoped undo returns business rows and Scheduling crosswalks to the post-MyTeam baseline.
        await using (var context = clone.Db())
        {
            var result = await new SsBulkUndo(context).UndoAsync(applied, ct);
            Assert.Empty(result.Blockers);
            Assert.NotNull(result.ReceiptRunId);
        }
        AssertEquivalent(baseline, await SnapshotAsync(clone, true, ct), compared, "bulk undo");
        await AssertNoDuplicatesOrOrphansAsync(clone, ct);
        await using (var context = clone.Db())
        {
            Assert.Equal(
                "RolledBack",
                (await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == applied, ct)).Status
            );
            var again = await new SsBulkUndo(context).UndoAsync(applied, ct);
            Assert.Contains("RunNotApplied", again.Blockers);
        }

        // The undone run can be re-applied cleanly and produces the same graph.
        var reapplied = await RunAsync(clone, ct);
        await AssertNoTechnicalFailureAsync(clone, reapplied, ct);
        await AssertNoDuplicatesOrOrphansAsync(clone, ct);
        AssertEquivalent(appliedState, await SnapshotAsync(clone, true, ct), BusinessSections, "re-apply after undo");

        // Undo lock cycles: an application transaction holds one scheduling table and, once undo waits for its
        // table locks, needs another that undo may already hold (in both directions, so the cycle exists whatever
        // order undo locks in). Undo's bounded acquisition backs off so PostgreSQL never aborts either side; the
        // writer commits and undo then completes.
        foreach (var (first, second) in new[] { ("AssignmentEntries", "Events"), ("Events", "AssignmentEntries") })
        {
            var undoCycle = await CopyAsync(
                clone,
                $"undo_cycle_{first.ToLowerInvariant()}",
                MigrationPersistenceMode.Bulk,
                ct
            );
            var appHolding = new TaskCompletionSource();
            var app = Task.Run(
                async () =>
                {
                    await using var connection = new NpgsqlConnection(undoCycle.ConnectionString);
                    await connection.OpenAsync(ct);
                    await using var tx = await connection.BeginTransactionAsync(ct);
                    await using (
                        var lockFirst = new NpgsqlCommand(
                            $"LOCK TABLE \"{first}\" IN ROW EXCLUSIVE MODE",
                            connection,
                            tx
                        )
                    )
                        await lockFirst.ExecuteNonQueryAsync(ct);
                    appHolding.SetResult();
                    var waited = Stopwatch.StartNew();
                    while (waited.Elapsed < TimeSpan.FromSeconds(30))
                    {
                        await using var probe = new NpgsqlCommand(
                            "SELECT count(*) FROM pg_locks WHERE NOT granted AND pid <> pg_backend_pid() AND database = (SELECT oid FROM pg_database WHERE datname = current_database())",
                            connection,
                            tx
                        );
                        if ((long)(await probe.ExecuteScalarAsync(ct))! > 0)
                            break;
                        await Task.Delay(10, ct);
                    }
                    Assert.True(waited.Elapsed < TimeSpan.FromSeconds(30), "undo never waited");
                    await using (
                        var lockSecond = new NpgsqlCommand(
                            $"LOCK TABLE \"{second}\" IN ROW EXCLUSIVE MODE",
                            connection,
                            tx
                        )
                    )
                        await lockSecond.ExecuteNonQueryAsync(ct);
                    await tx.CommitAsync(ct);
                },
                ct
            );
            await appHolding.Task.WaitAsync(TimeSpan.FromSeconds(60), ct);
            await using (var context = undoCycle.Db())
            {
                var result = await new SsBulkUndo(context).UndoAsync(reapplied, ct);
                Assert.Empty(result.Blockers);
                Assert.NotNull(result.ReceiptRunId);
            }
            await app;
            AssertEquivalent(baseline, await SnapshotAsync(undoCycle, true, ct), compared, $"undo lock cycle {first}");
        }

        // A later Unified row referencing an owned row (a new user on a migrated shift) blocks undo.
        await ExecuteAsync(
            clone.ConnectionString,
            """
            INSERT INTO "ShiftEntryUsers" ("ShiftEntryId","UserId","CreatedOn")
            SELECT s."Id", u."Id", now() FROM "ShiftEntries" s
            JOIN "DataMigrationRecords" r ON r."TargetType"='ShiftEntry' AND r."TargetKey"=s."Id"::text
              AND r."EntityType"='Shift' AND r."LegacyId"='910031'
            CROSS JOIN LATERAL (SELECT u."Id" FROM "Users" u WHERE u."Id"<>'00000000-0000-0000-0000-000000000001'
              AND NOT EXISTS (SELECT 1 FROM "ShiftEntryUsers" x WHERE x."ShiftEntryId"=s."Id" AND x."UserId"=u."Id")
              ORDER BY u."Id" LIMIT 1) u;
            """,
            ct
        );
        Assert.Equal(
            1,
            await CountAsync(
                clone.ConnectionString,
                "SELECT COUNT(*) FROM \"ShiftEntryUsers\" WHERE \"CreatedOn\" > (SELECT MAX(\"StartedOn\") FROM \"DataMigrationRuns\")",
                ct
            )
        );
        var dependent = await SnapshotAsync(clone, true, ct);
        await using (var context = clone.Db())
        {
            var blocked = await new SsBulkUndo(context).UndoAsync(reapplied, ct);
            Assert.Contains("OwnedTargetDrift", blocked.Blockers);
            Assert.Null(blocked.ReceiptRunId);
        }
        AssertEquivalent(dependent, await SnapshotAsync(clone, true, ct), compared, "blocked undo (later dependent)");
        await ExecuteAsync(
            clone.ConnectionString,
            "DELETE FROM \"ShiftEntryUsers\" WHERE \"CreatedOn\" > (SELECT MAX(\"StartedOn\") FROM \"DataMigrationRuns\")",
            ct
        );

        // A later DELETE of an owned child (a shift-entry link and its users) leaves every surviving row at a run
        // version; the recorded graph fingerprint still reveals it and undo refuses without changing anything.
        var deleted = await CopyAsync(clone, "undo_del", MigrationPersistenceMode.Bulk, ct);
        await ExecuteAsync(
            deleted.ConnectionString,
            """
            WITH link AS (
              SELECT min(l."Id") AS "Id" FROM "ShiftAssignmentEntries" l
              JOIN "DataMigrationRecords" r ON r."TargetType"='AssignmentEntry'
                AND r."TargetKey"=l."AssignmentEntryId"::text AND r."Source"='SS'
            ), users AS (
              DELETE FROM "ShiftAssignmentEntryUsers" WHERE "ShiftAssignmentEntryId" = (SELECT "Id" FROM link)
            )
            DELETE FROM "ShiftAssignmentEntries" WHERE "Id" = (SELECT "Id" FROM link)
            """,
            ct
        );
        const string countLinks = "SELECT count(*) FROM \"ShiftAssignmentEntries\"";
        Assert.Equal(
            await CountAsync(clone.ConnectionString, countLinks, ct) - 1,
            await CountAsync(deleted.ConnectionString, countLinks, ct)
        );
        var withoutLink = await SnapshotAsync(deleted, true, ct);
        await using (var context = deleted.Db())
        {
            var blocked = await new SsBulkUndo(context).UndoAsync(reapplied, ct);
            Assert.Null(blocked.ReceiptRunId);
            Assert.Equal(["OwnedTargetDrift"], blocked.Blockers);
        }
        AssertEquivalent(withoutLink, await SnapshotAsync(deleted, true, ct), compared, "blocked undo (deleted link)");

        // A Unified edit to an owned row (the event of a migrated shift) blocks undo and nothing changes.
        await ExecuteAsync(clone.ConnectionString, AdverseTargetDrift, ct);
        var edited = await SnapshotAsync(clone, true, ct);
        await using (var context = clone.Db())
        {
            var blocked = await new SsBulkUndo(context).UndoAsync(reapplied, ct);
            Assert.Null(blocked.ReceiptRunId);
            Assert.Equal(["OwnedTargetDrift"], blocked.Blockers);
        }
        AssertEquivalent(edited, await SnapshotAsync(clone, true, ct), compared, "blocked undo (drift)");
    }

    // The Bulk simulation mirrors SchedulingConcurrencyInterceptor without referencing Unified.Scheduling; pin the
    // interceptor name it detects and the event vocabulary it copies so a rename cannot silently break parity.
    [Fact]
    public void BulkSimulation_MirrorsSchedulingConcurrencyInterceptorVocabulary()
    {
        Assert.Equal("SchedulingConcurrencyInterceptor", typeof(SchedulingConcurrencyInterceptor).Name);
        Assert.Equal(
            global::Unified.Scheduling.SchedulingConstants.SourceModule,
            SsSchedulingSim.SchedulingSourceModule
        );
        Assert.Equal(
            global::Unified.Scheduling.SchedulingConstants.ShiftEventTypeCode,
            SsSchedulingSim.ShiftEventTypeCode
        );
        Assert.Equal(
            global::Unified.Scheduling.SchedulingConstants.AssignmentEventTypeCode,
            SsSchedulingSim.AssignmentEventTypeCode
        );
    }

    // ------------------------------------------------------------------ PerRow rerun guards

    // Pins the PerRow rules that decide whether a source row still needs transfer. Rerun speed-ups must keep every
    // assertion here: nothing is inserted twice, unchanged rows stay untouched, changed rows take the same update or
    // review path, rows without a crosswalk are re-evaluated, and missing capture receipts are recaptured.
    [Theory(Skip = TemplatesSkipReason, SkipUnless = nameof(AreTemplatesConfigured))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PerRowRerun_KeepsDuplicateAndChangeDetectionGuards(bool planReplacementFamiliesOnly)
    {
        var ct = TestContext.Current.CancellationToken;
        EnableAudit();
        SsPhaseLog.Observer = ObservePhase;
        var fixture = await AdverseFixtureAsync(ct);
        var clone = await CloneAsync(fixture, "guard", MigrationPersistenceMode.PerRow, ct, chunk: 7, batch: 5);
        clone.Options.Sources.SS.Jobs.Scheduling.PlanReplacementFamiliesOnly = planReplacementFamiliesOnly;
        // With the switch on, planning is skipped exactly when no captured family needs graph replacement.
        void AssertPlanSkipped(Guid run, bool expected) =>
            Assert.Equal(planReplacementFamiliesOnly && expected, PlanSkipped(run));

        // Round 1 then an unchanged rerun.
        var first = await RunAsync(clone, ct);
        await AssertNoTechnicalFailureAsync(clone, first, ct);
        AssertPlanSkipped(first, true);
        var firstOutcomes = await OutcomesAsync(clone, first, ct);
        var business = await SnapshotAsync(clone, true, ct);
        var records = await RecordRowsAsync(clone, ct);
        var rerun = await RunAsync(clone, ct);
        await AssertNoTechnicalFailureAsync(clone, rerun, ct);
        await AssertRerunAsync(clone, rerun, ct);
        AssertPlanSkipped(rerun, true);
        AssertEquivalent(business, await SnapshotAsync(clone, true, ct), BusinessSections, "unchanged rerun");
        Assert.Equal(records, await RecordRowsAsync(clone, ct));
        await AssertNoDuplicatesOrOrphansAsync(clone, ct);
        var rerunOutcomes = await OutcomesAsync(clone, rerun, ct);
        Assert.Equal(
            firstOutcomes.Keys.Where(x => IsGraphType(x.Type)).Order(),
            rerunOutcomes.Keys.Where(x => IsGraphType(x.Type)).Order()
        );
        foreach (var (key, outcome) in rerunOutcomes.Where(x => IsGraphType(x.Key.Type)))
        {
            // Transferred rows resolve through their crosswalk; every other row gets the same verdict again.
            var original = firstOutcomes[key];
            if (IsBlocked(original) || outcome == original)
                Assert.Equal(original, outcome);
            else
                Assert.True(
                    outcome.EndsWith(":ExistingCrosswalk", StringComparison.Ordinal)
                        || outcome.EndsWith(":ExistingGroupCrosswalk", StringComparison.Ordinal),
                    $"rerun {key}: {outcome} (round 1 {original})"
                );
        }
        Assert.Equal("Migrated:ExistingCrosswalk", rerunOutcomes[("Duty", "900000")]);
        Assert.Equal("Merged:ExistingGroupCrosswalk", rerunOutcomes[("Shift", "900000")]);
        Assert.Equal("Migrated:ExistingGroupCrosswalk", rerunOutcomes[("Assignment", "900000")]);

        // Crash after the write commit and before capture: drop the capture receipts of two components.
        var receipts = await ReceiptRowsAsync(clone, ct);
        Assert.NotEmpty(receipts);
        await ExecuteAsync(
            clone.ConnectionString,
            """
            WITH lost AS (
              SELECT "TargetType","TargetKey" FROM "DataMigrationRecords"
              WHERE "Source"='SS' AND "EntityType"='SchedulingComponent'
              ORDER BY "TargetType","TargetKey" LIMIT 2),
            gone AS (
              SELECT r."Id" FROM "DataMigrationRecords" r JOIN lost l
                ON l."TargetType"=r."TargetType" AND l."TargetKey"=r."TargetKey"
              WHERE r."Source"='SS' AND r."EntityType" IN ('SchedulingComponent','SchedulingMember'))
            , m AS (DELETE FROM "DataMigrationMutations" WHERE "DataMigrationRecordId" IN (SELECT "Id" FROM gone))
            DELETE FROM "DataMigrationRecords" WHERE "Id" IN (SELECT "Id" FROM gone);
            """,
            ct
        );
        Assert.True((await ReceiptRowsAsync(clone, ct)).Count < receipts.Count);
        var recapture = await RunAsync(clone, ct);
        await AssertNoTechnicalFailureAsync(clone, recapture, ct);
        await AssertRerunAsync(clone, recapture, ct);
        AssertPlanSkipped(recapture, true);
        Assert.Equal(receipts, await ReceiptRowsAsync(clone, ct));
        AssertEquivalent(business, await SnapshotAsync(clone, true, ct), BusinessSections[..^1], "recapture");
        await AssertNoDuplicatesOrOrphansAsync(clone, ct);

        // Round 2: a Unified edit without a source change, a linked-only source change and a new graph.
        await ExecuteAsync(clone.ConnectionString, AdverseTargetDrift, ct);
        await ExecuteAsync(fixture.Source, AdverseRound2Delta, ct);
        var second = await RunAsync(clone, ct);
        await AssertNoTechnicalFailureAsync(clone, second, ct);
        var secondOutcomes = await OutcomesAsync(clone, second, ct);
        // A Unified edit with no source change is left alone; the new graph is inserted.
        Assert.Equal("Migrated:ExistingGroupCrosswalk", secondOutcomes[("Shift", "910030")]);
        Assert.Equal("Migrated:ExistingCrosswalk", secondOutcomes[("CourtRoom", "900000")]);
        Assert.Equal("Migrated:", secondOutcomes[("Shift", "910040")]);
        Assert.Equal("Migrated:", secondOutcomes[("Duty", "910040")]);
        Assert.Equal("Migrated:", secondOutcomes[("DutySlot", "910040")]);
        await AssertNoDuplicatesOrOrphansAsync(clone, ct);
        var afterSecond = await RecordRowsAsync(clone, ct);
        var again = await RunAsync(clone, ct);
        await AssertNoTechnicalFailureAsync(clone, again, ct);
        await AssertRerunAsync(clone, again, ct);
        Assert.Equal(afterSecond, await RecordRowsAsync(clone, ct));
        Assert.Equal(
            1,
            await CountAsync(
                clone.ConnectionString,
                """SELECT COUNT(*) FROM "DataMigrationRecords" WHERE "EntityType"='Shift' AND "LegacyId"='910040'""",
                ct
            )
        );

        // Round 3: an UpdatedOn-only touch, a change on a Unified-edited target, a clean owned change and a new
        // future duty that joins an existing recurrence (graph replacement and rebinding).
        await ExecuteAsync(fixture.Source, AdverseRound3Delta, ct);
        var third = await RunAsync(clone, ct);
        await AssertNoTechnicalFailureAsync(clone, third, ct);
        AssertPlanSkipped(third, false);
        var thirdOutcomes = await OutcomesAsync(clone, third, ct);
        Assert.Equal("Migrated:OwnedFullGroupUpdate", thirdOutcomes[("Shift", "900002")]);
        Assert.Equal("Rejected:GraphReplacementRequiresInverseJournal", thirdOutcomes[("Shift", "910030")]);
        Assert.Equal("Rejected:GraphReplacementRequiresInverseJournal", thirdOutcomes[("Shift", "910031")]);
        Assert.Equal("Migrated:", thirdOutcomes[("Duty", "910050")]);
        Assert.Equal("Failed:MaterialMissingShiftUserLink", thirdOutcomes[("DutySlot", "910050")]);
        // Rows migrated earlier and now rejected keep their crosswalk, so only the duplicate checks apply here.
        await AssertNoDuplicatesOrOrphansAsync(clone, ct, rejectedOwnNothing: false);
        var afterThird = await RecordRowsAsync(clone, ct);
        Assert.Contains(
            afterThird,
            x =>
                x.StartsWith("Shift:900002|", StringComparison.Ordinal)
                && x.EndsWith("|2026-11-21T00:00:00Z", StringComparison.Ordinal)
        );
        var settled = await RunAsync(clone, ct);
        await AssertNoTechnicalFailureAsync(clone, settled, ct);
        await AssertRerunAsync(clone, settled, ct);
        Assert.Equal(afterThird, await RecordRowsAsync(clone, ct));
        await AssertNoDuplicatesOrOrphansAsync(clone, ct, rejectedOwnNothing: false);
        var settledOutcomes = await OutcomesAsync(clone, settled, ct);
        // Rejected and failed rows are reported again on every rerun; they are never treated as transferred.
        foreach (var (key, outcome) in thirdOutcomes.Where(x => IsGraphType(x.Key.Type) && IsBlocked(x.Value)))
            Assert.Equal(outcome, settledOutcomes.GetValueOrDefault(key));
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, bool> SkippedPlans = new();

    private static void ObservePhase(Guid run, string phase, string state, long? units)
    {
        if (phase == "Plan" && state == "Skipped")
            SkippedPlans[run] = true;
    }

    private static bool PlanSkipped(Guid run) => SkippedPlans.ContainsKey(run);

    // Every rerun speed-up switch. A switch may only skip work whose result is already proven, so turning
    // them all on must leave the migrated state identical to the full path.
    private static void EnableRerunOptimisations(DataMigrationOptions options)
    {
        options.Sources.SS.Jobs.Scheduling.PlanReplacementFamiliesOnly = true;
        options.Sources.SS.Jobs.Scheduling.PrefetchReplayRecords = true;
    }

    // Simulates a crash after the write commit and before capture: two components lose their receipts.
    private const string DropTwoComponentReceipts = """
        WITH lost AS (
          SELECT "TargetType","TargetKey" FROM "DataMigrationRecords"
          WHERE "Source"='SS' AND "EntityType"='SchedulingComponent'
          ORDER BY "LegacyId" LIMIT 2),
        gone AS (
          SELECT r."Id" FROM "DataMigrationRecords" r JOIN lost l
            ON l."TargetType"=r."TargetType" AND l."TargetKey"=r."TargetKey"
          WHERE r."Source"='SS' AND r."EntityType" IN ('SchedulingComponent','SchedulingMember'))
        , m AS (DELETE FROM "DataMigrationMutations" WHERE "DataMigrationRecordId" IN (SELECT "Id" FROM gone))
        DELETE FROM "DataMigrationRecords" WHERE "Id" IN (SELECT "Id" FROM gone);
        """;

    [Fact(Skip = TemplatesSkipReason, SkipUnless = nameof(AreTemplatesConfigured))]
    public async Task RerunOptimisations_LeaveStateIdenticalToFullPath_AcrossRounds()
    {
        var ct = TestContext.Current.CancellationToken;
        EnableAudit();
        EnableRollbackJournal();
        SsPhaseLog.Observer = ObservePhase;
        var fixture = await AdverseFixtureAsync(ct);
        var full = await CloneAsync(fixture, "eq_full", MigrationPersistenceMode.PerRow, ct, chunk: 7, batch: 5);
        var fast = await CloneAsync(fixture, "eq_fast", MigrationPersistenceMode.PerRow, ct, chunk: 7, batch: 5);
        var bulkFull = await CloneAsync(fixture, "eq_bfull", MigrationPersistenceMode.Bulk, ct, chunk: 7, batch: 5);
        var bulkFast = await CloneAsync(fixture, "eq_bfast", MigrationPersistenceMode.Bulk, ct, chunk: 7, batch: 5);
        EnableRerunOptimisations(fast.Options);
        EnableRerunOptimisations(bulkFast.Options);

        async Task<Guid> RoundAsync(Clone expected, Clone actual, string label)
        {
            var perRow = expected.Options.Sources.SS.Jobs.Scheduling.PersistenceMode == MigrationPersistenceMode.PerRow;
            var expectedBefore = await TargetKeysAsync(expected, ct);
            var actualBefore = await TargetKeysAsync(actual, ct);
            var expectedCrosswalksBefore = perRow ? await CrosswalkIdentitiesAsync(expected, ct) : null;
            var actualCrosswalksBefore = perRow ? await CrosswalkIdentitiesAsync(actual, ct) : null;
            var expectedRun = await RunAsync(expected, ct);
            var actualRun = await RunAsync(actual, ct);
            await AssertNoTechnicalFailureAsync(expected, expectedRun, ct);
            await AssertNoTechnicalFailureAsync(actual, actualRun, ct);
            var snapshot = await SnapshotAsync(expected, true, ct);
            AssertEquivalent(snapshot, await SnapshotAsync(actual, true, ct), snapshot.Keys, label);
            // Snapshots normalise target IDs, so also prove the same targets were kept (not deleted and re-inserted).
            var expectedRebound = Rebound(expectedBefore, await TargetKeysAsync(expected, ct));
            Assert.Equal(expectedRebound, Rebound(actualBefore, await TargetKeysAsync(actual, ct)));
            // Rebinding only happens in graph replacement, which reads the plan: it must never have been skipped.
            if (expectedRebound.Count > 0)
                Assert.False(PlanSkipped(actualRun), $"{label}: planning skipped although a graph was replaced");
            var expectedJournal = await JournalRowsAsync(expected, expectedRun, ct);
            var actualJournal = await JournalRowsAsync(actual, actualRun, ct);
            if (perRow)
            {
                await using var modelContext = Db(expected.ConnectionString);
                var comparison = new JournalComparison(
                    modelContext.Model,
                    expected.BaselineMax,
                    await JournalRunLabelsAsync(expected, ct),
                    await JournalRunLabelsAsync(actual, ct),
                    label
                );
                comparison.PairCrosswalks(expectedCrosswalksBefore!, actualCrosswalksBefore!);
                comparison.PairCrosswalks(
                    await CrosswalkIdentitiesAsync(expected, ct),
                    await CrosswalkIdentitiesAsync(actual, ct)
                );
                comparison.AssertEquivalent(expectedJournal, actualJournal);
            }
            else
            {
                Assert.Empty(expectedJournal);
                Assert.Empty(actualJournal);
            }
            if (label == "changed captured component")
            {
                Assert.Contains("Shift:910031", expectedRebound);
                Assert.Contains(
                    expectedJournal,
                    x => x.Table == "ShiftEntries" && x.Operation == "D" && x.BeforeImage is not null
                );
            }
            return actualRun;
        }

        // Bulk never reads the switches; it must stay identical with them set.
        await AssertNotRefusedAsync(bulkFast, await RoundAsync(bulkFull, bulkFast, "bulk first apply"), ct);

        await RoundAsync(full, fast, "first apply");
        await RoundAsync(full, fast, "no-change rerun");
        await ExecuteAsync(full.ConnectionString, DropTwoComponentReceipts, ct);
        await ExecuteAsync(fast.ConnectionString, DropTwoComponentReceipts, ct);
        await RoundAsync(full, fast, "resume after capture failure");
        // A Unified edit, a renamed (reference) court room and a new graph.
        var driftSql = AdverseTargetDrift.Replace("now()", "@updatedOn", StringComparison.Ordinal);
        Assert.Contains("@updatedOn", driftSql);
        var driftOn = DateTimeOffset.UtcNow;
        await ExecuteAsync(full.ConnectionString, driftSql, ct, new NpgsqlParameter("updatedOn", driftOn));
        await ExecuteAsync(fast.ConnectionString, driftSql, ct, new NpgsqlParameter("updatedOn", driftOn));
        await ExecuteAsync(fixture.Source, AdverseRound2Delta, ct);
        await RoundAsync(full, fast, "reference target changed");
        await RoundAsync(full, fast, "rerun after reference change");
        // Owned changes to captured components: graph replacement and rebinding.
        await ExecuteAsync(fixture.Source, AdverseRound3Delta, ct);
        await RoundAsync(full, fast, "changed captured component");
        await RoundAsync(full, fast, "settled rerun");
        // A new shift overlapping a captured group: an owned in-place group merge.
        await ExecuteAsync(fixture.Source, OverlappingShiftJoinsCapturedGroup, ct);
        await RoundAsync(full, fast, "captured group merge");
        await RoundAsync(full, fast, "settled after group merge");
        // Changes outside graph replacement: replayed from the per-row record, never from a stale page.
        await ExecuteAsync(fixture.Source, """UPDATE "Duty" SET "UpdatedOn"='2026-11-23Z' WHERE "Id"=910040;""", ct);
        var touched = await RoundAsync(full, fast, "duty touched");
        Assert.Equal("Migrated:UnchangedMappedSource", (await OutcomesAsync(fast, touched, ct))[("Duty", "910040")]);
        await ExecuteAsync(
            fixture.Source,
            """UPDATE "Duty" SET "Comment"='round 5 changed',"UpdatedOn"='2026-11-24Z' WHERE "Id"=910040;""",
            ct
        );
        var changed = await RoundAsync(full, fast, "duty comment changed");
        Assert.Equal("Migrated:OwnedMappedUpdate", (await OutcomesAsync(fast, changed, ct))[("Duty", "910040")]);
        await RoundAsync(full, fast, "settled after duty change");
    }

    private const string OverlappingShiftJoinsCapturedGroup = """
        INSERT INTO "Shift" ("Id","StartDate","EndDate","LocationId","CreatedOn","SheriffId","Timezone","OvertimeHours","Comment")
        VALUES (910041,'2026-11-16T15:00Z','2026-11-16T18:00Z',900000,'2026-11-22Z','11111111-1111-4111-8111-111111111111','Etc/UTC',0,'round 4 overlap');
        """;

    private static async Task<Dictionary<string, string>> TargetKeysAsync(Clone clone, CancellationToken ct) => (
            await TextRowsAsync(
                clone,
                """
                SELECT "EntityType"||':'||"LegacyId"||'|'||"TargetType"||':'||"TargetKey"
                FROM "DataMigrationRecords" WHERE "Source"='SS' AND NOT "IsDeleted"
                  AND "EntityType" NOT IN ('SchedulingComponent','SchedulingMember')
                """,
                ct
            )
        ).Select(x => x.Split('|')).ToDictionary(x => x[0], x => x[1]);

    // Legacy rows that existed before the round and now point at a different (or no) target.
    private static List<string> Rebound(Dictionary<string, string> before, Dictionary<string, string> after) =>
        before.Where(x => !after.TryGetValue(x.Key, out var now) || now != x.Value).Select(x => x.Key).Order().ToList();

    private sealed record CrosswalkIdentity(
        long Id,
        string TargetType,
        string TargetKey,
        string PayloadHash,
        string? AfterHash
    );

    private static async Task<Dictionary<(string Type, string Id), CrosswalkIdentity>> CrosswalkIdentitiesAsync(
        Clone clone,
        CancellationToken ct
    )
    {
        await using var db = Db(clone.ConnectionString);
        var records = await db
            .DataMigrationRecords.AsNoTracking()
            .Where(x => x.Source == "SS")
            .Select(x => new
            {
                x.Id,
                x.EntityType,
                x.LegacyId,
                x.TargetType,
                x.TargetKey,
                x.PayloadHash,
                x.AfterHash,
            })
            .ToListAsync(ct);
        return records.ToDictionary(
            x => (x.EntityType, x.LegacyId),
            x => new CrosswalkIdentity(x.Id, x.TargetType, x.TargetKey, x.PayloadHash, x.AfterHash)
        );
    }

    private sealed record JournalWindow(string Run, DateTimeOffset StartedOn, DateTimeOffset CompletedOn);

    private sealed record JournalLabels(
        Dictionary<Guid, string> Runs,
        Dictionary<DateTimeOffset, string> Starts,
        IReadOnlyList<JournalWindow> Windows
    )
    {
        internal string? RunAt(DateTimeOffset timestamp)
        {
            string? found = null;
            foreach (var window in Windows)
            {
                if (timestamp < window.StartedOn || timestamp > window.CompletedOn)
                    continue;
                if (found is not null)
                    return null; // Overlapping windows are ambiguous and cannot normalize different values.
                found = window.Run;
            }
            return found;
        }
    }

    private static async Task<JournalLabels> JournalRunLabelsAsync(Clone clone, CancellationToken ct)
    {
        await using var db = Db(clone.ConnectionString);
        var runs = await db
            .DataMigrationRuns.AsNoTracking()
            .Where(x => x.Source == "SS" && !x.DryRun)
            .OrderBy(x => x.StartedOn)
            .Select(x => new
            {
                x.Id,
                x.Job,
                x.StartedOn,
                x.CompletedOn,
            })
            .ToListAsync(ct);
        var ids = new Dictionary<Guid, string>();
        var starts = new Dictionary<DateTimeOffset, string>();
        var windows = new List<JournalWindow>();
        foreach (var group in runs.GroupBy(x => x.Job))
        {
            var ordinal = 0;
            foreach (var run in group)
            {
                var name = $"{group.Key}#{++ordinal}";
                Assert.True(run.StartedOn is not null && run.CompletedOn is not null, $"{name}: incomplete run window");
                var start = run.StartedOn!.Value.ToUniversalTime();
                var end = run.CompletedOn!.Value.ToUniversalTime();
                Assert.True(end >= start, $"{name}: invalid run window");
                ids.Add(run.Id, name);
                Assert.True(starts.TryAdd(start, name), $"{name}: ambiguous run start");
                windows.Add(new JournalWindow(name, start, end));
            }
        }
        return new JournalLabels(ids, starts, windows);
    }

    private sealed record JournalRow(
        Guid KeyId,
        string Schema,
        string Table,
        string Operation,
        string RowKey,
        string? BeforeImage,
        string AfterHash,
        string AfterVersion,
        string[] ChangedColumns,
        bool BeforeHadPhoto,
        bool PhotoChanged
    );

    private static async Task<List<JournalRow>> JournalRowsAsync(Clone clone, Guid run, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(clone.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            """
            SELECT "KeyId","SchemaName","TableName","Operation","RowKey"::text,
                   CASE WHEN "BeforeImage" IS NULL THEN NULL ELSE pgp_sym_decrypt("BeforeImage",@key) END,
                   "AfterHash","AfterVersion","ChangedColumns","BeforeHadPhoto","PhotoChanged"
            FROM "DataMigrationJournals" WHERE "RunId"=@run ORDER BY "Id"
            """,
            connection
        );
        // Only the fixed synthetic test key from EnableRollbackJournal is used.
        command.Parameters.AddWithValue("key", Convert.ToBase64String(new byte[32]));
        command.Parameters.AddWithValue("run", run);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<JournalRow>();
        while (await reader.ReadAsync(ct))
            rows.Add(
                new JournalRow(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.GetString(6),
                    reader.GetString(7),
                    reader.GetFieldValue<string[]>(8),
                    reader.GetBoolean(9),
                    reader.GetBoolean(10)
                )
            );
        return rows;
    }

    // Compare the actual ordered inverse operations and decrypted images, not mutation nullness. IDs are paired
    // bijectively and anchored by crosswalks before AND after the run, including deleted/replaced graph targets.
    // Scheduling row/graph saves flush before the final run completion. In these isolated clones there are no
    // concurrent target editors, and the sole out-of-run edit uses the same timestamp on both sides. A matching
    // run window is therefore treated as a generated UpdatedOn, not as proof of timestamp provenance generally.
    // Other normalization is limited to run starts, generated IDs, xmin and hashes that incorporate those IDs.
    private sealed class JournalComparison(
        IModel model,
        Dictionary<string, long> baseline,
        JournalLabels leftRuns,
        JournalLabels rightRuns,
        string label
    )
    {
        private readonly Dictionary<string, IEntityType> tables = model
            .GetEntityTypes()
            .Where(x => x.GetTableName() is not null)
            .GroupBy(x => x.GetTableName()!)
            .ToDictionary(x => x.Key, x => x.First());
        private readonly Dictionary<string, string> targetTables = BusinessTypes.ToDictionary(
            x => x.Name,
            x => model.FindEntityType(x)!.GetTableName()!
        );
        private readonly Dictionary<(string Table, string Id), string> forward = [];
        private readonly Dictionary<(string Table, string Id), string> reverse = [];

        private bool DynamicTable(string table) =>
            targetTables.ContainsValue(table)
            || table
                is "DataMigrationRecords"
                    or "DataMigrationMutations"
                    or "DataMigrationWorkItems"
                    or "DataMigrationCheckpoints";

        private void Check(bool condition, string what) => Assert.True(condition, $"{label}: {what}");

        private void Pair(string table, string left, string right)
        {
            var business = BusinessTypes.FirstOrDefault(x => targetTables[x.Name] == table);
            if (
                business is not null
                && baseline.TryGetValue(business.Name, out var floor)
                && long.TryParse(left, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)
                && long.TryParse(right, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)
                && (l <= floor || r <= floor)
            )
            {
                Check(l == r, $"{table} baseline identity differs");
                return;
            }
            Check(
                !forward.TryGetValue((table, left), out var priorRight) || priorRight == right,
                $"{table} ID mapping differs"
            );
            Check(
                !reverse.TryGetValue((table, right), out var priorLeft) || priorLeft == left,
                $"{table} ID mapping is not one-to-one"
            );
            forward[(table, left)] = right;
            reverse[(table, right)] = left;
        }

        internal void PairCrosswalks(
            Dictionary<(string Type, string Id), CrosswalkIdentity> left,
            Dictionary<(string Type, string Id), CrosswalkIdentity> right
        )
        {
            Check(left.Keys.ToHashSet().SetEquals(right.Keys), "crosswalk identities differ");
            foreach (var (key, a) in left)
            {
                var b = right[key];
                Check(a.TargetType == b.TargetType, $"crosswalk target type differs for {key.Type}:{key.Id}");
                Pair(
                    "DataMigrationRecords",
                    a.Id.ToString(CultureInfo.InvariantCulture),
                    b.Id.ToString(CultureInfo.InvariantCulture)
                );
                if (targetTables.TryGetValue(a.TargetType, out var table))
                    Pair(table, a.TargetKey, b.TargetKey);
                else
                    Check(a.TargetKey == b.TargetKey, $"crosswalk target differs for {key.Type}:{key.Id}");
                Check(
                    (a.AfterHash is null) == (b.AfterHash is null),
                    $"crosswalk hash presence differs for {key.Type}:{key.Id}"
                );
                if (a.AfterHash is not null)
                    Pair("target-after-hash", a.AfterHash, b.AfterHash!);
                if (key.Type is "Duty" or "DutySlot" or "DutyEntry" or "AssignmentSeries")
                    Pair("id-bound-payload-hash", a.PayloadHash, b.PayloadHash);
                else
                    Check(a.PayloadHash == b.PayloadHash, $"crosswalk payload differs for {key.Type}:{key.Id}");
            }
        }

        internal void AssertEquivalent(IReadOnlyList<JournalRow> left, IReadOnlyList<JournalRow> right)
        {
            Check(left.Count == right.Count, "inverse journal operation count differs");
            for (var i = 0; i < left.Count; i++)
            {
                var a = left[i];
                var b = right[i];
                Check(
                    a.KeyId == b.KeyId && a.Schema == b.Schema && a.Table == b.Table && a.Operation == b.Operation,
                    $"inverse journal operation {i} differs"
                );
                Check(a.ChangedColumns.SequenceEqual(b.ChangedColumns), $"{a.Table} changed columns differ");
                Check(
                    a.BeforeHadPhoto == b.BeforeHadPhoto && a.PhotoChanged == b.PhotoChanged,
                    $"{a.Table} photo flags differ"
                );
                Check(
                    (a.AfterVersion == "missing") == (b.AfterVersion == "missing"),
                    $"{a.Table} version state differs"
                );
                Check((a.AfterHash == "missing") == (b.AfterHash == "missing"), $"{a.Table} after-image state differs");
                Pair("journal-after-hash", a.AfterHash, b.AfterHash);
                CompareObject(a.Table, a.RowKey, b.RowKey);
                if (a.Operation == "I")
                    Check(a.BeforeImage is null && b.BeforeImage is null, $"{a.Table} insert has a before-image");
                else
                {
                    Check(a.BeforeImage is not null && b.BeforeImage is not null, $"{a.Table} before-image missing");
                    CompareObject(a.Table, a.BeforeImage!, b.BeforeImage!);
                }
            }
        }

        private void CompareObject(string table, string left, string right)
        {
            using var a = JsonDocument.Parse(left);
            using var b = JsonDocument.Parse(right);
            var aFields = a.RootElement.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal).ToList();
            var bFields = b.RootElement.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal).ToList();
            Check(
                aFields.Select(x => x.Name).SequenceEqual(bFields.Select(x => x.Name)),
                $"{table} image columns differ"
            );
            for (var i = 0; i < aFields.Count; i++)
                CompareValue(table, a.RootElement, b.RootElement, aFields[i].Name, aFields[i].Value, bFields[i].Value);
        }

        private void CompareValue(
            string table,
            JsonElement leftRow,
            JsonElement rightRow,
            string column,
            JsonElement left,
            JsonElement right
        )
        {
            Check(left.ValueKind == right.ValueKind, $"{table}.{column} image kind differs");
            if (left.ValueKind == JsonValueKind.Null)
                return;
            var a = left.ValueKind == JsonValueKind.String ? left.GetString()! : left.GetRawText();
            var b = right.ValueKind == JsonValueKind.String ? right.GetString()! : right.GetRawText();
            tables.TryGetValue(table, out var entity);
            var property = entity?.GetProperties().FirstOrDefault(x => x.GetColumnName() == column);
            if (property?.IsPrimaryKey() == true && DynamicTable(table))
            {
                Pair(table, a, b);
                return;
            }
            var principal = property
                ?.GetContainingForeignKeys()
                .Select(x => x.PrincipalEntityType.GetTableName())
                .FirstOrDefault(x => x is not null && DynamicTable(x));
            if (principal is not null)
            {
                Pair(principal, a, b);
                return;
            }
            if (table == "DataMigrationRecords" && column == "TargetKey")
            {
                var target = leftRow.GetProperty("TargetType").GetString()!;
                Check(target == rightRow.GetProperty("TargetType").GetString(), "crosswalk image target type differs");
                if (targetTables.TryGetValue(target, out var targetTable))
                {
                    Pair(targetTable, a, b);
                    return;
                }
            }
            if (
                table == "DataMigrationRecords" && column == "AfterHash"
                || table == "DataMigrationMutations" && column == "AfterHash"
            )
            {
                Pair("target-after-hash", a, b);
                return;
            }
            if (
                table == "DataMigrationRecords"
                && column == "PayloadHash"
                && leftRow.GetProperty("EntityType").GetString()
                    is "Duty"
                        or "DutySlot"
                        or "DutyEntry"
                        or "AssignmentSeries"
            )
            {
                Pair("id-bound-payload-hash", a, b);
                return;
            }
            if (table == "DataMigrationMutations" && column == "Version")
                return; // PostgreSQL xmin differs between transactions.
            if (Guid.TryParse(a, out var aGuid) && Guid.TryParse(b, out var bGuid))
            {
                var foundLeft = leftRuns.Runs.TryGetValue(aGuid, out var leftRun);
                var foundRight = rightRuns.Runs.TryGetValue(bGuid, out var rightRun);
                if (foundLeft || foundRight)
                {
                    Check(foundLeft && foundRight && leftRun == rightRun, $"{table}.{column} run differs");
                    return;
                }
            }
            var type = Nullable.GetUnderlyingType(property?.ClrType ?? typeof(object)) ?? property?.ClrType;
            if (
                (type == typeof(DateTimeOffset) || type == typeof(DateTime))
                && DateTimeOffset.TryParse(
                    a,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var aTime
                )
                && DateTimeOffset.TryParse(
                    b,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var bTime
                )
            )
            {
                if (aTime == bTime)
                    return;
                if (column == "UpdatedOn")
                {
                    var leftWindow = leftRuns.RunAt(aTime);
                    var rightWindow = rightRuns.RunAt(bTime);
                    Check(leftWindow is not null && leftWindow == rightWindow, $"{table}.{column} run window differs");
                    return;
                }
                var foundLeft = leftRuns.Starts.TryGetValue(aTime.ToUniversalTime(), out var leftStart);
                var foundRight = rightRuns.Starts.TryGetValue(bTime.ToUniversalTime(), out var rightStart);
                if (foundLeft || foundRight)
                {
                    Check(
                        foundLeft && foundRight && leftStart == rightStart,
                        $"{table}.{column} run timestamp differs"
                    );
                    return;
                }
            }
            Check(a == b, $"{table}.{column} before-image differs");
        }
    }

    [Fact]
    public void JournalComparison_DetectsMissingAndChangedBeforeImages()
    {
        using var db = new SqliteTestUnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite("DataSource=:memory:").Options
        );
        var labels = new JournalLabels([], [], []);
        JournalComparison Comparison() => new(db.Model, [], labels, labels, "synthetic");
        var original = new JournalRow(
            Guid.Empty,
            "public",
            "Events",
            "D",
            """{"Id":10}""",
            """{"Id":10,"Title":"original","EventSeriesId":null,"UpdatedOn":"2040-01-01T00:00:01Z"}""",
            "missing",
            "missing",
            [],
            false,
            false
        );
        var equal = original with
        {
            RowKey = """{"Id":20}""",
            BeforeImage = """{"Id":20,"Title":"original","EventSeriesId":null,"UpdatedOn":"2040-01-01T00:00:01Z"}""",
        };
        Comparison().AssertEquivalent([original], [equal]);
        var changed = Assert.ThrowsAny<Exception>(() =>
            Comparison()
                .AssertEquivalent(
                    [original],
                    [
                        equal with
                        {
                            BeforeImage =
                                """{"Id":20,"Title":"changed","EventSeriesId":null,"UpdatedOn":"2040-01-01T00:00:01Z"}""",
                        },
                    ]
                )
        );
        Assert.Contains("Events.Title", changed.Message);
        var changedUpdatedOn = Assert.ThrowsAny<Exception>(() =>
            Comparison()
                .AssertEquivalent(
                    [original],
                    [
                        equal with
                        {
                            BeforeImage =
                                """{"Id":20,"Title":"original","EventSeriesId":null,"UpdatedOn":"2040-01-01T00:00:02Z"}""",
                        },
                    ]
                )
        );
        Assert.Contains("Events.UpdatedOn", changedUpdatedOn.Message);
        var missing = Assert.ThrowsAny<Exception>(() =>
            Comparison().AssertEquivalent([original], [equal with { BeforeImage = null }])
        );
        Assert.Contains("before-image missing", missing.Message);

        var leftStart = new DateTimeOffset(2026, 11, 25, 0, 0, 0, TimeSpan.Zero);
        var rightStart = leftStart.AddSeconds(1);
        var leftLabels = new JournalLabels([], new() { [leftStart] = "Scheduling#1" }, []);
        var rightLabels = new JournalLabels([], new() { [rightStart] = "Scheduling#1" }, []);
        new JournalComparison(db.Model, [], leftLabels, rightLabels, "run timestamps").AssertEquivalent(
            [
                original with
                {
                    BeforeImage =
                        """{"Id":10,"Title":"original","EventSeriesId":null,"CreatedOn":"2026-11-25T00:00:00Z"}""",
                },
            ],
            [
                equal with
                {
                    BeforeImage =
                        """{"Id":20,"Title":"original","EventSeriesId":null,"CreatedOn":"2026-11-25T00:00:01Z"}""",
                },
            ]
        );
    }

    [Fact]
    public void JournalComparison_NormalizesUpdatedOnOnlyWithinMatchingRunWindows()
    {
        using var db = new SqliteTestUnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite("DataSource=:memory:").Options
        );
        var start = new DateTimeOffset(2026, 11, 25, 0, 0, 0, TimeSpan.Zero);
        var leftFirst = new JournalWindow("Scheduling#1", start, start.AddMinutes(1));
        var leftSecond = new JournalWindow("Scheduling#2", start.AddMinutes(10), start.AddMinutes(11));
        var rightFirst = new JournalWindow("Scheduling#1", start.AddMinutes(2), start.AddMinutes(3));
        var rightSecond = new JournalWindow("Scheduling#2", start.AddMinutes(12), start.AddMinutes(13));
        var leftLabels = new JournalLabels([], [], [leftFirst, leftSecond]);
        var rightLabels = new JournalLabels([], [], [rightFirst, rightSecond]);
        JournalComparison Comparison() => new(db.Model, [], leftLabels, rightLabels, "windows");
        JournalRow Row(int id, DateTimeOffset updatedOn) =>
            new(
                Guid.Empty,
                "public",
                "Events",
                "D",
                JsonSerializer.Serialize(new { Id = id }),
                JsonSerializer.Serialize(new { Id = id, UpdatedOn = updatedOn }),
                "missing",
                "missing",
                [],
                false,
                false
            );

        Comparison()
            .AssertEquivalent(
                [Row(10, leftFirst.StartedOn.AddSeconds(10))],
                [Row(20, rightFirst.StartedOn.AddSeconds(30))]
            );
        Comparison().AssertEquivalent([Row(10, leftFirst.StartedOn)], [Row(20, rightFirst.CompletedOn)]);
        // An identical out-of-run edit must not be rejected just because one clone's window contains its value.
        var shared = leftFirst.StartedOn.AddSeconds(10);
        Comparison().AssertEquivalent([Row(10, shared)], [Row(20, shared)]);

        var arbitrary = Assert.ThrowsAny<Exception>(() =>
            Comparison().AssertEquivalent([Row(10, start.AddDays(-2))], [Row(20, start.AddDays(-1))])
        );
        Assert.Contains("Events.UpdatedOn", arbitrary.Message);
        var differentRuns = Assert.ThrowsAny<Exception>(() =>
            Comparison()
                .AssertEquivalent(
                    [Row(10, leftFirst.StartedOn.AddSeconds(10))],
                    [Row(20, rightSecond.StartedOn.AddSeconds(10))]
                )
        );
        Assert.Contains("Events.UpdatedOn", differentRuns.Message);
        var outsideWindow = Assert.ThrowsAny<Exception>(() =>
            Comparison()
                .AssertEquivalent(
                    [Row(10, leftFirst.StartedOn.AddSeconds(10))],
                    [Row(20, rightFirst.CompletedOn.AddSeconds(1))]
                )
        );
        Assert.Contains("Events.UpdatedOn", outsideWindow.Message);

        var overlap = leftLabels with
        {
            Windows =
            [
                leftFirst,
                new JournalWindow("Scheduling#2", leftFirst.StartedOn.AddSeconds(5), leftFirst.CompletedOn),
            ],
        };
        var ambiguous = Assert.ThrowsAny<Exception>(() =>
            new JournalComparison(db.Model, [], overlap, rightLabels, "overlap").AssertEquivalent(
                [Row(10, leftFirst.StartedOn.AddSeconds(10))],
                [Row(20, rightFirst.StartedOn.AddSeconds(10))]
            )
        );
        Assert.Contains("Events.UpdatedOn", ambiguous.Message);
    }

    [Fact(Skip = TemplatesSkipReason, SkipUnless = nameof(AreTemplatesConfigured))]
    public async Task ReplayPrefetch_NeverAnswersForARecordTrackedInTheJob()
    {
        var ct = TestContext.Current.CancellationToken;
        var fixture = await AdverseFixtureAsync(ct);
        var clone = await CloneAsync(fixture, "prefetch", MigrationPersistenceMode.PerRow, ct);
        await AssertNoTechnicalFailureAsync(clone, await RunAsync(clone, ct), ct);
        await using var db = Db(clone.ConnectionString);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var ids = await db
            .DataMigrationRecords.Where(x => x.Source == "SS" && x.EntityType == "Shift")
            .OrderBy(x => x.LegacyId)
            .Select(x => x.LegacyId)
            .Take(3)
            .ToListAsync(ct);
        Assert.Equal(3, ids.Count);
        var (preTracked, plain, written) = (ids[0], ids[1], ids[2]);
        const string absent = "999999999";
        var rows = new Dictionary<string, List<SsRow>>
        {
            ["Shift"] =
            [
                .. ids.Append(absent).Select(id => new SsRow(new Dictionary<string, object?> { ["Id"] = id })),
            ],
        };
        Task<global::Unified.Db.Models.DataMigration.DataMigrationRecord> Tracked(string id) =>
            db.DataMigrationRecords.SingleAsync(
                x => x.Source == "SS" && x.EntityType == "Shift" && x.LegacyId == id,
                ct
            );
        await Tracked(preTracked);

        using var prefetch = new SsReplayPrefetch(db, rows);
        Assert.False((await prefetch.TryGetAsync("Shift", plain, false, ct)).Known);
        var (known, entry) = await prefetch.TryGetAsync("Shift", plain, true, ct);
        Assert.True(known);
        Assert.Equal((await Tracked(plain)).PayloadHash, entry!.PayloadHash);
        Assert.Equal((true, null), await prefetch.TryGetAsync("Shift", absent, false, ct));
        Assert.False((await prefetch.TryGetAsync("Shift", preTracked, false, ct)).Known);
        // A write rolled back by a savepoint and a cleared tracker still never trusts the page.
        Assert.True((await prefetch.TryGetAsync("Shift", written, false, ct)).Known);
        await tx.CreateSavepointAsync("plan", ct);
        (await Tracked(written)).PayloadHash = "changed";
        await db.SaveChangesAsync(ct);
        await tx.RollbackToSavepointAsync("plan", ct);
        db.ChangeTracker.Clear();
        Assert.False((await prefetch.TryGetAsync("Shift", written, true, ct)).Known);
        Assert.False((await prefetch.TryGetAsync("Shift", plain, true, ct)).Known);
        Assert.Equal(1, prefetch.PageLoads);
    }

    private static bool IsGraphType(string type) => type is "Shift" or "Duty" or "DutySlot" or "Assignment";

    private static bool IsBlocked(string outcome) =>
        outcome.StartsWith("Rejected:", StringComparison.Ordinal)
        || outcome.StartsWith("Failed:", StringComparison.Ordinal)
        || outcome.StartsWith("NeedsAction:", StringComparison.Ordinal);

    private static async Task<List<string>> RecordRowsAsync(Clone clone, CancellationToken ct) =>
        await TextRowsAsync(
            clone,
            """
            SELECT "EntityType"||':'||"LegacyId"||'|'||"TargetType"||':'||"TargetKey"||'|'||COALESCE("PayloadHash",'')
              ||'|'||COALESCE(to_char("SourceUpdatedAtUtc" AT TIME ZONE 'UTC','YYYY-MM-DD"T"HH24:MI:SS"Z"'),'')
            FROM "DataMigrationRecords" WHERE "Source"='SS' AND NOT "IsDeleted"
              AND "EntityType" NOT IN ('SchedulingComponent','SchedulingMember')
            ORDER BY 1
            """,
            ct
        );

    private static async Task<List<string>> ReceiptRowsAsync(Clone clone, CancellationToken ct) =>
        await TextRowsAsync(
            clone,
            """
            SELECT "EntityType"||':'||"LegacyId"||'|'||"TargetType"||':'||"TargetKey"||'|'||COALESCE("PayloadHash",'')
            FROM "DataMigrationRecords" WHERE "Source"='SS' AND NOT "IsDeleted"
              AND "EntityType" IN ('SchedulingComponent','SchedulingMember')
            ORDER BY 1
            """,
            ct
        );

    private static async Task<List<string>> TextRowsAsync(Clone clone, string sql, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(clone.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<string>();
        while (await reader.ReadAsync(ct))
            rows.Add(reader.GetString(0));
        return rows;
    }

    // Benchmark (opt-in): UNIFIED_MIGRATION_SCHEDULING_BENCHMARK_TEMPLATE=<frozen source database>.
    // UNIFIED_MIGRATION_SCHEDULING_BENCHMARK_PERROW=false skips the PerRow leg (then extrapolate).
    // UNIFIED_MIGRATION_SCHEDULING_BENCHMARK_SOURCE_DELTA=<sql> is applied to the cloned source first.
    [Fact(
        Skip = "Set " + BenchmarkVariable + " to benchmark PerRow vs Bulk.",
        SkipUnless = nameof(IsBenchmarkConfigured)
    )]
    public async Task Benchmark_PerRowVsBulk()
    {
        var ct = TestContext.Current.CancellationToken;
        EnableAudit();
        var output = TestContext.Current.TestOutputHelper;
        var template = Environment.GetEnvironmentVariable(BenchmarkVariable)!;
        var runPerRow = !string.Equals(
            Environment.GetEnvironmentVariable("UNIFIED_MIGRATION_SCHEDULING_BENCHMARK_PERROW"),
            "false",
            StringComparison.OrdinalIgnoreCase
        );
        // Optional SQL applied to the cloned source (never the template), e.g. to repair MyTeam-blocking
        // reference data in an adverse production-shape fixture so Scheduling lineage can be satisfied.
        var sourceDelta = Environment.GetEnvironmentVariable("UNIFIED_MIGRATION_SCHEDULING_BENCHMARK_SOURCE_DELTA");
        var fixture = await FixtureAsync(template, string.IsNullOrWhiteSpace(sourceDelta) ? null : sourceDelta, ct);
        var bulk = await CloneAsync(fixture, "bench_b", MigrationPersistenceMode.Bulk, ct, chunk: 2000, batch: 100);
        var watch = Stopwatch.StartNew();
        var bulkRun = await RunAsync(bulk, ct);
        var bulkElapsed = watch.Elapsed;
        await AssertNoTechnicalFailureAsync(bulk, bulkRun, ct);
        await AssertNotRefusedAsync(bulk, bulkRun, ct);
        await AssertNoDuplicatesOrOrphansAsync(bulk, ct);
        await using (var context = bulk.Db())
        {
            var run = await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == bulkRun, ct);
            output?.WriteLine(
                $"{template}: scanned={run.ScannedCount} inserted={run.InsertedCount} rejected={run.RejectedCount} "
                    + $"chunks={run.CommittedBatchCount}"
            );
            output?.WriteLine(
                $"Bulk: {bulkElapsed.TotalSeconds:F1} s ({run.ScannedCount / bulkElapsed.TotalSeconds:F0} rows/s)"
            );
        }
        if (!runPerRow)
            return;
        var perRow = await CloneAsync(fixture, "bench_a", MigrationPersistenceMode.PerRow, ct, chunk: 2000, batch: 100);
        watch.Restart();
        var perRowRun = await RunAsync(perRow, ct);
        var perRowElapsed = watch.Elapsed;
        await AssertNoTechnicalFailureAsync(perRow, perRowRun, ct);
        output?.WriteLine($"PerRow: {perRowElapsed.TotalSeconds:F1} s");
        var expected = await SnapshotAsync(perRow, false, ct);
        AssertEquivalent(expected, await SnapshotAsync(bulk, false, ct), expected.Keys, "benchmark");
    }

    private async Task<Guid> CancelPerRowAsync(Clone clone, long graphRecords, CancellationToken ct)
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await using var context = clone.Db();
        var accepted = await Operations(context, clone.Options).TriggerAsync("SS", "Scheduling", false, actor, ct);
        await using var worker = clone.Db(new CommitCounter(clone.ConnectionString, graphRecords, source));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            Executor(worker, clone.Options).ExecuteAcquiredAsync(accepted.RunId, source.Token)
        );
        await using var check = clone.Db();
        var run = await check.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == accepted.RunId, ct);
        Assert.Equal("Failed", run.Status);
        Assert.Equal(
            "Canceled",
            (await check.DataMigrationFailures.AsNoTracking().SingleAsync(x => x.RunId == accepted.RunId, ct)).ErrorType
        );
        return accepted.RunId;
    }

    // Cancels the run at the first committed PerRow batch after which at least `threshold` Scheduling graph
    // crosswalks exist, i.e. strictly between batch boundaries of a real run.
    private sealed class CommitCounter(string connectionString, long threshold, CancellationTokenSource source)
        : DbTransactionInterceptor
    {
        public override async Task TransactionCommittedAsync(
            System.Data.Common.DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default
        )
        {
            if (source.IsCancellationRequested)
                return;
            if (
                await CountAsync(
                    connectionString,
                    "SELECT COUNT(*) FROM \"DataMigrationRecords\" WHERE \"EntityType\" IN ('Shift','Duty','DutySlot')",
                    CancellationToken.None
                ) >= threshold
            )
                await source.CancelAsync();
        }
    }

    private static async Task<Dictionary<(string Type, string Id), string>> OutcomesAsync(
        Clone clone,
        Guid runId,
        CancellationToken ct
    )
    {
        await using var context = clone.Db();
        return (
            await context
                .DataMigrationOutcomes.AsNoTracking()
                .Where(x => x.RunId == runId)
                .Select(x => new
                {
                    x.EntityType,
                    x.LegacyId,
                    x.Outcome,
                    x.Reason,
                })
                .ToListAsync(ct)
        ).ToDictionary(x => (x.EntityType, x.LegacyId), x => x.Outcome + ":" + x.Reason);
    }

    /// <summary>
    /// No half graph: every crosswalk member set per target in <paramref name="partial"/> equals a member set
    /// of the complete <paramref name="reference"/> run (a merge group or duty graph is all-or-nothing).
    /// </summary>
    private static async Task AssertWholeGraphsAsync(Clone partial, Clone reference, CancellationToken ct)
    {
        const string sql = """
            SELECT string_agg("EntityType"||':'||"LegacyId", ',' ORDER BY "EntityType","LegacyId")
            FROM "DataMigrationRecords"
            WHERE "Source"='SS' AND "EntityType" IN ('Shift','Duty','DutySlot','DutyEntry','AssignmentSeries')
            GROUP BY "TargetType","TargetKey"
            """;
        async Task<List<string>> Groups(Clone clone)
        {
            await using var connection = new NpgsqlConnection(clone.ConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var result = new List<string>();
            while (await reader.ReadAsync(ct))
                result.Add(reader.GetString(0));
            return result;
        }
        var complete = (await Groups(reference)).ToHashSet(StringComparer.Ordinal);
        var half = (await Groups(partial)).Where(x => !complete.Contains(x)).ToList();
        Assert.True(half.Count == 0, "Half graphs: " + string.Join(" | ", half));
    }

    private async Task<Clone> CopyAsync(Clone source, string name, MigrationPersistenceMode mode, CancellationToken ct)
    {
        NpgsqlConnection.ClearAllPools();
        var template = new NpgsqlConnectionStringBuilder(source.ConnectionString).Database!;
        var connection = await CreateDatabaseAsync(
            $"migration_it_sched_{name}_{databases.Count}_{suffix}",
            template,
            ct
        );
        var ss = source.Options.Sources.SS;
        return new Clone(
            new NpgsqlConnectionStringBuilder(connection) { Pooling = true }.ConnectionString,
            NewOptions(ss.ConnectionString!, mode, ss.Jobs.Scheduling.BulkChunkSize, ss.BatchSize),
            source.BaselineMax
        )
        {
            AuditFloor = source.AuditFloor,
        };
    }

    // ------------------------------------------------------------------ fixture

    private sealed record Fixture(string Source, string Base, Dictionary<string, long> BaselineMax);

    /// <summary>
    /// Clones the source template, applies an optional delta, then prepares ONE migrated + seeded target
    /// with a genuinely completed MyTeam run (PerRow, the real executor path) used as the clone template.
    /// </summary>
    private async Task<Fixture> FixtureAsync(
        string sourceTemplate,
        string? sourceDelta,
        CancellationToken ct,
        string? targetDelta = null
    )
    {
        var id = databases.Count.ToString(CultureInfo.InvariantCulture);
        var source = await CreateDatabaseAsync($"migration_source_sched_{id}_{suffix}", sourceTemplate, ct);
        if (sourceDelta is not null)
            await ExecuteAsync(source, sourceDelta, ct);
        var baseName = $"migration_it_sched_base_{id}_{suffix}";
        var target = await CreateDatabaseAsync(baseName, await SeededTargetAsync(ct), ct);
        var baseClone = new Clone(target, NewOptions(source, MigrationPersistenceMode.PerRow, 100, 100), new());
        var myTeam = await RunAsync(baseClone, ct, job: "MyTeam");
        await AssertRunAsync(baseClone, myTeam, "Completed", ct);
        if (targetDelta is not null)
            await ExecuteAsync(target, targetDelta, ct);
        var max = new Dictionary<string, long>();
        await using (var db = Db(target))
            foreach (var type in BusinessTypes)
                max[type.Name] = await CountAsync(
                    target,
                    $"SELECT COALESCE(MAX(\"Id\"),0) FROM \"{db.Model.FindEntityType(type)!.GetTableName()}\"",
                    ct
                );
        NpgsqlConnection.ClearAllPools();
        return new(source, baseName, max);
    }

    // Several production seeders add static seed instances directly to the context, so they can only be run
    // once per process; seed ONE migrated target per test process and clone it (CREATE DATABASE TEMPLATE).
    private static readonly SemaphoreSlim SeedLock = new(1, 1);
    private static string? seededTarget;

    private static async Task<string> SeededTargetAsync(CancellationToken ct)
    {
        await SeedLock.WaitAsync(ct);
        try
        {
            if (seededTarget is not null)
                return seededTarget;
            var name = $"migration_it_sched_seeded_{Environment.ProcessId}_{Guid.NewGuid():N}"[..60];
            var admin = new NpgsqlConnectionStringBuilder(Admin) { Pooling = false };
            await using (var connection = new NpgsqlConnection(admin.ConnectionString))
            {
                await connection.OpenAsync(ct);
                await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
                await command.ExecuteNonQueryAsync(ct);
            }
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                NpgsqlConnection.ClearAllPools();
                using var connection = new NpgsqlConnection(admin.ConnectionString);
                connection.Open();
                using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", connection);
                command.ExecuteNonQuery();
            };
            var target = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = name }.ConnectionString;
            await using (var db = Db(target))
            {
                await db.Database.MigrateAsync(ct);
                db.Users.Add(
                    new User
                    {
                        Id = User.SystemUser,
                        IdirName = "SYSTEM",
                        IsEnabled = false,
                        FirstName = "System",
                        LastName = "System",
                    }
                );
                await db.SaveChangesAsync(ct);
                // Production startup runs the registered seeders (MigrationAndSeedService) with the configured
                // seed data sets; run the reference seeders the SS jobs read (default roles + permissions,
                // calendar/scheduling/leave/loan-out event types, stats taxonomy), in production order.
                var dataSets = UserManagementSeedDataSets
                    .All.Where(x =>
                        x.Key
                            is UserManagementSeedDataSets.DefaultRolesDataSet
                                or UserManagementSeedDataSets.UserManagementPermissionsDataSet
                    )
                    .SelectMany(x => x.Configurations)
                    .ToList();
                SeederBase<UnifiedDbContext>[] seeders =
                [
                    new PermissionSeeder(
                        NullLogger<PermissionSeeder>.Instance,
                        dataSets.OfType<PermissionSeedConfiguration>()
                    ),
                    new RoleSeeder(NullLogger<RoleSeeder>.Instance, dataSets.OfType<RoleSeedConfiguration>()),
                    new EventTypeSeeder(NullLogger<EventTypeSeeder>.Instance),
                    new EventStatusTypeSeeder(NullLogger<EventStatusTypeSeeder>.Instance),
                    new ShiftEventTypeSeeder(NullLogger<ShiftEventTypeSeeder>.Instance),
                    new SchedulingLeaveEventTypeSeeder(NullLogger<SchedulingLeaveEventTypeSeeder>.Instance),
                    new LoanOutEventTypeSeeder(NullLogger<LoanOutEventTypeSeeder>.Instance),
                    new LeaveTypeSeeder(NullLogger<LeaveTypeSeeder>.Instance),
                    new PositionTypeSeeder(NullLogger<PositionTypeSeeder>.Instance),
                    new StatGroupSeeder(NullLogger<StatGroupSeeder>.Instance),
                    new StatCategorySeeder(NullLogger<StatCategorySeeder>.Instance),
                    new SubCategorySeeder(NullLogger<SubCategorySeeder>.Instance),
                ];
                foreach (var seeder in seeders.OrderBy(x => x.Order).ThenBy(x => x.Name, StringComparer.Ordinal))
                    await seeder.SeedAsync(db, ct);
            }
            NpgsqlConnection.ClearAllPools();
            return seededTarget = name;
        }
        finally
        {
            SeedLock.Release();
        }
    }

    private async Task<Clone> CloneAsync(
        Fixture fixture,
        string name,
        MigrationPersistenceMode mode,
        CancellationToken ct,
        int chunk = 7,
        int batch = 5
    )
    {
        var connection = await CreateDatabaseAsync(
            $"migration_it_sched_{name}_{databases.Count}_{suffix}",
            fixture.Base,
            ct
        );
        var clone = new Clone(
            new NpgsqlConnectionStringBuilder(connection) { Pooling = true }.ConnectionString,
            NewOptions(fixture.Source, mode, chunk, batch),
            fixture.BaselineMax
        );
        clone.AuditFloor = await CountAsync(
            clone.ConnectionString,
            "SELECT COALESCE(MAX(\"Id\"),0) FROM \"AuditRecords\"",
            ct
        );
        return clone;
    }

    private sealed class Clone(string connectionString, DataMigrationOptions options, Dictionary<string, long> baseline)
    {
        public string ConnectionString => connectionString;
        public DataMigrationOptions Options => options;
        public Dictionary<string, long> BaselineMax => baseline;
        public long AuditFloor { get; set; }

        // Production contexts stamp CreatedById/UpdatedById through AuditableEntityInterceptor.
        public UnifiedDbContext Db(params IInterceptor[] extra) =>
            new(
                new DbContextOptionsBuilder<UnifiedDbContext>()
                    .UseNpgsql(connectionString)
                    // Production order: SchedulingModule registers its concurrency interceptor before
                    // AddInterceptors() adds the auditable-entity stamper (SaveRules/PostSave have no Scheduling work).
                    .AddInterceptors([
                        new SchedulingConcurrencyInterceptor(),
                        new AuditableEntityInterceptor(new SystemActor()),
                        .. extra,
                    ])
                    .Options
            );
    }

    private static DataMigrationOptions NewOptions(string source, MigrationPersistenceMode mode, int chunk, int batch)
    {
        var options = new DataMigrationOptions();
        options.Sources.SS.Enabled = true;
        options.Sources.SS.ConnectionString = source;
        options.Sources.SS.CutoverDate = new DateOnly(2026, 12, 1);
        options.Sources.SS.BatchSize = batch;
        foreach (var job in DataMigrationOperations.Jobs)
        {
            options.Sources.SS.Jobs.For(job).Enabled = true;
            options.Sources.SS.Jobs.For(job).AllowMigrationApply = true;
            options.Sources.SS.Jobs.For(job).BatchSize = batch;
        }
        options.Sources.SS.Jobs.Scheduling.PersistenceMode = mode;
        options.Sources.SS.Jobs.Scheduling.BulkChunkSize = chunk;
        return options;
    }

    // ------------------------------------------------------------------ execution

    private static readonly AuditRecordOptions AuditOptions = new();

    private static void EnableAudit()
    {
        global::Audit.Core.Configuration.AuditDisabled = false;
        AuditOptions.ExcludedPropertyNames = new AuditRecordOptions().ExcludedPropertyNames;
        var action = new AuditRecordEntityAction(new SystemActor(), AuditOptions);
        global::Audit
            .Core.Configuration.Setup()
            .UseEntityFramework(ef =>
                ef.AuditTypeMapper(_ => typeof(AuditRecord))
                    .AuditEntityAction<AuditRecord>(action.Populate)
                    .IgnoreMatchedProperties(true)
            );
        global::Audit
            .EntityFramework.Configuration.Setup()
            .ForContext<UnifiedDbContext>()
            .UseOptOut()
            .Ignore<AuditRecord>();
    }

    private sealed class SystemActor : ICurrentActorResolver
    {
        public CurrentActor Resolve() => new(User.SystemUser, "System");
    }

    private SsMigrationExecutor Executor(
        UnifiedDbContext context,
        DataMigrationOptions options,
        SsSchedulingBulkWriter? bulk = null
    ) =>
        new(
            context,
            configuration,
            MsOptions.Create(options),
            flags,
            new DataMigrationControlService(context),
            new SsSnapshotReader(MsOptions.Create(options)),
            new SsMyTeamWriter(context, new SsLeaveWriter(context)),
            new SsTrainingWriter(context),
            new SsSchedulingWriter(context),
            rollbackJournal: new SsRollbackJournal(context, configuration),
            trainingBulk: new SsTrainingBulkWriter(context, MsOptions.Create(AuditOptions)),
            schedulingBulk: bulk ?? new SsSchedulingBulkWriter(context, MsOptions.Create(AuditOptions))
        );

    private DataMigrationOperations Operations(UnifiedDbContext context, DataMigrationOptions options)
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(UnifiedClaimTypes.UserId, actor.ToString())], "test")
        );
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        return new(
            context,
            MsOptions.Create(options),
            flags,
            new DataMigrationControlService(context),
            new Dispatcher(Executor(context, options)),
            null!,
            http,
            new Authorization()
        );
    }

    private async Task<Guid> RunAsync(
        Clone clone,
        CancellationToken ct,
        string job = "Scheduling",
        bool dryRun = false,
        Func<int, CancellationToken, Task>? hook = null,
        bool expectFailure = false,
        params IInterceptor[] interceptors
    )
    {
        await using var context = clone.Db();
        var accepted = await Operations(context, clone.Options).TriggerAsync("SS", job, dryRun, actor, ct);
        await using var worker = clone.Db(interceptors);
        var writer = new SsSchedulingBulkWriter(worker, MsOptions.Create(AuditOptions))
        {
            BeforeChunkCommitAsync = hook,
        };
        var execute = () => Executor(worker, clone.Options, writer).ExecuteAcquiredAsync(accepted.RunId, ct);
        if (expectFailure)
            await Assert.ThrowsAnyAsync<Exception>(execute);
        else
        {
            try
            {
                await execute();
            }
            catch (Exception error)
            {
                await using var evidence = clone.Db();
                var failures = await evidence
                    .DataMigrationFailures.AsNoTracking()
                    .Where(x => x.RunId == accepted.RunId)
                    .Select(x => x.EntityType + ":" + x.LegacyId + ":" + x.ErrorType + ":" + x.ErrorMessage)
                    .ToListAsync(CancellationToken.None);
                throw new InvalidOperationException(
                    $"{job} ({clone.Options.Sources.SS.Jobs.Scheduling.PersistenceMode}) failed: "
                        + string.Join("; ", failures),
                    error
                );
            }
        }
        return accepted.RunId;
    }

    // ------------------------------------------------------------------ assertions

    // Bulk writes no per-row audits; the run leaves only its trigger record and one run summary.
    private async Task AssertBulkRunSummaryOnlyAsync(Clone clone, Guid runId, CancellationToken ct)
    {
        await using var db = clone.Db();
        var run = await db.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == runId, ct);
        var audits = await db.AuditRecords.AsNoTracking().Where(x => x.Id > clone.AuditFloor).ToListAsync(ct);
        Assert.DoesNotContain(audits, x => x.EntityType is not ("DataMigrationOperation" or "DataMigrationRunSummary"));
        Assert.Single(audits, x => x.EntityType == "DataMigrationOperation");
        var summary = Assert.Single(audits, x => x.EntityType == "DataMigrationRunSummary");
        Assert.Equal(runId.ToString("D"), summary.EntityPK);
        Assert.Equal(actor, summary.ActorUserId);

        using var document = JsonDocument.Parse(summary.NewValues!);
        var values = document.RootElement;
        Assert.Equal(runId, values.GetProperty("RunId").GetGuid());
        Assert.Equal("Scheduling", values.GetProperty("Job").GetString());
        Assert.Equal(nameof(MigrationPersistenceMode.Bulk), values.GetProperty("PersistenceMode").GetString());
        var counts = values.GetProperty("Counts");
        Assert.Equal(run.ScannedCount, counts.GetProperty(nameof(run.ScannedCount)).GetInt32());
        Assert.Equal(run.InsertedCount, counts.GetProperty(nameof(run.InsertedCount)).GetInt32());
        Assert.Equal(run.CommittedBatchCount, counts.GetProperty(nameof(run.CommittedBatchCount)).GetInt32());
        Assert.True(run.ScannedCount > 0, "Scheduling Bulk summary has zero scanned rows");
        Assert.True(run.InsertedCount > 0, "Scheduling Bulk summary has zero inserted rows");
    }

    private static async Task AssertNoTechnicalFailureAsync(Clone clone, Guid runId, CancellationToken ct)
    {
        await using var context = clone.Db();
        var run = await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == runId, ct);
        var failures = await context
            .DataMigrationFailures.AsNoTracking()
            .Where(x => x.RunId == runId)
            .Select(x => x.ErrorType + ":" + x.ErrorMessage)
            .ToListAsync(ct);
        Assert.True(failures.Count == 0, $"{run.Job} run technical failures: {string.Join("; ", failures)}");
        Assert.NotEqual("Running", run.Status);
        Assert.NotNull(run.CompletedOn);
    }

    private static async Task AssertNotRefusedAsync(Clone clone, Guid runId, CancellationToken ct)
    {
        await using var context = clone.Db();
        var refusal = await context
            .DataMigrationOutcomes.AsNoTracking()
            .Where(x => x.RunId == runId && x.EntityType == "SchedulingBulk")
            .Select(x => x.Reason)
            .ToListAsync(ct);
        Assert.True(refusal.Count == 0, "Bulk refused: " + string.Join("; ", refusal));
    }

    private static async Task AssertRerunAsync(Clone clone, Guid runId, CancellationToken ct)
    {
        await using var context = clone.Db();
        var run = await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == runId, ct);
        Assert.Equal(0, run.InsertedCount);
        Assert.Equal(0, run.UpdatedCount);
    }

    private static async Task AssertRunAsync(Clone clone, Guid runId, string status, CancellationToken ct)
    {
        await using var context = clone.Db();
        var run = await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == runId, ct);
        if (run.Status != status)
        {
            var details = await context
                .DataMigrationOutcomes.AsNoTracking()
                .Where(x => x.RunId == runId && x.Outcome != "Migrated" && x.Outcome != "Excluded")
                .Select(x => x.EntityType + ":" + x.LegacyId + ":" + x.Outcome + ":" + x.Reason)
                .Take(40)
                .ToListAsync(ct);
            var failures = await context
                .DataMigrationFailures.AsNoTracking()
                .Where(x => x.RunId == runId)
                .Select(x => x.ErrorType + ":" + x.ErrorMessage)
                .ToListAsync(ct);
            Assert.Fail($"{run.Job} run {run.Status}: {string.Join("; ", details.Concat(failures))}");
        }
    }

    private static async Task AssertNoDuplicatesOrOrphansAsync(
        Clone clone,
        CancellationToken ct,
        bool rejectedOwnNothing = true
    )
    {
        foreach (
            var sql in new[]
            {
                """
                    SELECT COUNT(*) FROM (SELECT 1 FROM "DataMigrationRecords"
                    GROUP BY "Source","EntityType","LegacyId" HAVING COUNT(*) > 1) d
                    """,
                // Every crosswalked business target exists.
                """
                    SELECT COUNT(*) FROM "DataMigrationRecords" r WHERE r."Source"='SS' AND NOT r."IsDeleted" AND (
                      (r."TargetType"='ShiftEntry' AND NOT EXISTS (SELECT 1 FROM "ShiftEntries" t WHERE t."Id"::text=r."TargetKey"))
                      OR (r."TargetType"='AssignmentEntry' AND NOT EXISTS (SELECT 1 FROM "AssignmentEntries" t WHERE t."Id"::text=r."TargetKey"))
                      OR (r."TargetType"='AssignmentDefinition' AND NOT EXISTS (SELECT 1 FROM "AssignmentDefinitions" t WHERE t."Id"::text=r."TargetKey"))
                      OR (r."TargetType"='CourtRoom' AND NOT EXISTS (SELECT 1 FROM "CourtRooms" t WHERE t."Id"::text=r."TargetKey")))
                    """,
                // No duplicate shift users / assignment links / link users.
                """
                    SELECT COUNT(*) FROM (SELECT 1 FROM "ShiftEntryUsers" GROUP BY "ShiftEntryId","UserId" HAVING COUNT(*) > 1) d
                    """,
                """
                    SELECT COUNT(*) FROM (SELECT 1 FROM "ShiftAssignmentEntries"
                    GROUP BY "ShiftEntryId","AssignmentEntryId" HAVING COUNT(*) > 1) d
                    """,
                """
                    SELECT COUNT(*) FROM (SELECT 1 FROM "ShiftAssignmentEntryUsers"
                    GROUP BY "ShiftAssignmentEntryId","UserId" HAVING COUNT(*) > 1) d
                    """,
                // Rejected/failed Duty or DutySlot sources own no target (no half graph).
                """
                    SELECT COUNT(*) FROM "DataMigrationRecords" r JOIN LATERAL (
                      SELECT o."Outcome" FROM "DataMigrationOutcomes" o JOIN "DataMigrationRuns" u ON u."Id"=o."RunId"
                      WHERE o."EntityType"=r."EntityType" AND o."LegacyId"=r."LegacyId" AND u."Job"='Scheduling'
                      ORDER BY u."StartedOn" DESC LIMIT 1) last ON true
                    WHERE r."EntityType" IN ('Duty','DutySlot','Shift') AND r."CreatedByRunId" IN
                      (SELECT "Id" FROM "DataMigrationRuns" WHERE "Job"='Scheduling')
                      AND last."Outcome" IN ('Rejected','Failed') AND NOT r."IsDeleted"
                    """,
                // Events owned by nothing (orphaned by a partial graph).
                """
                    SELECT COUNT(*) FROM "Events" e WHERE e."SourceModule"='scheduling'
                    AND NOT EXISTS (SELECT 1 FROM "ShiftEntries" s WHERE s."EventId"=e."Id")
                    AND NOT EXISTS (SELECT 1 FROM "AssignmentEntries" a WHERE a."EventId"=e."Id")
                    """,
            }
        )
        {
            if (!rejectedOwnNothing && sql.Contains("JOIN LATERAL", StringComparison.Ordinal))
                continue;
            Assert.True(await CountAsync(clone.ConnectionString, sql, ct) == 0, "Integrity check failed: " + sql);
        }
    }

    // ------------------------------------------------------------------ canonical snapshot

    private static void AssertEquivalent(
        Dictionary<string, List<string>> expected,
        Dictionary<string, List<string>> actual,
        IEnumerable<string> sections,
        string label
    )
    {
        var problems = new List<string>();
        foreach (var section in sections)
        {
            var left = expected[section];
            var right = actual[section];
            var missing = Except(left, right);
            var extra = Except(right, left);
            if (missing.Count == 0 && extra.Count == 0)
                continue;
            problems.Add($"[{section}] only PerRow/expected ({missing.Count}):");
            problems.AddRange(missing.Take(12).Select(x => "  - " + x));
            problems.Add($"[{section}] only Bulk/actual ({extra.Count}):");
            problems.AddRange(extra.Take(12).Select(x => "  + " + x));
        }
        if (problems.Count > 0)
            Assert.Fail($"{label} parity mismatch:\n{string.Join('\n', problems)}");
    }

    private static List<string> Except(List<string> left, List<string> right)
    {
        var remaining = right.GroupBy(x => x, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Count());
        var result = new List<string>();
        foreach (var item in left)
        {
            if (remaining.TryGetValue(item, out var count) && count > 0)
                remaining[item] = count - 1;
            else
                result.Add(item);
        }
        return result;
    }

    /// <summary>
    /// Canonical target + evidence state. Generated integer IDs are replaced by labels: the crosswalk
    /// (EntityType:LegacyId set) for owned targets, <c>base:id</c> for rows that existed in the cloned
    /// baseline (identical in every clone), and structural owners for receipt-less children (event of a
    /// shift, link of a shift/entry pair, user of a link). Run IDs and run-start timestamps become job
    /// ordinals. Fingerprints that embed generated IDs (AfterHash; Duty/DutySlot payload) compare by presence.
    /// </summary>
    private static async Task<Dictionary<string, List<string>>> SnapshotAsync(
        Clone clone,
        bool collapseRuns,
        CancellationToken ct
    )
    {
        await using var connection = new NpgsqlConnection(clone.ConnectionString);
        await connection.OpenAsync(ct);
        async Task<List<object?[]>> Query(string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 600 };
            await using var reader = await command.ExecuteReaderAsync(ct);
            var rows = new List<object?[]>();
            while (await reader.ReadAsync(ct))
            {
                var values = new object?[reader.FieldCount];
                for (var i = 0; i < values.Length; i++)
                    values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(values);
            }
            return rows;
        }

        var runLabels = new Dictionary<Guid, string>();
        var startLabels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (
            var group in (
                await Query("""SELECT "Id","Job","StartedOn","DryRun" FROM "DataMigrationRuns" ORDER BY "StartedOn" """)
            )
                .Where(x => x[3] is false)
                .GroupBy(x => (string)x[1]!)
        )
        {
            var ordinal = 0;
            foreach (var run in group)
            {
                ordinal++;
                var label = collapseRuns ? group.Key : $"{group.Key}#{ordinal}";
                runLabels[(Guid)run[0]!] = label;
                if (run[2] is DateTime started)
                    startLabels.TryAdd(Iso(started), label);
            }
        }
        var userLabels = (
            await Query("""SELECT "TargetKey","LegacyId" FROM "DataMigrationRecords" WHERE "TargetType"='User' """)
        ).ToDictionary(x => (string)x[0]!, x => "U:" + (string)x[1]!, StringComparer.OrdinalIgnoreCase);
        foreach (var user in await Query("""SELECT "Id"::text,"IdirName" FROM "Users" """))
            userLabels.TryAdd((string)user[0]!, "idir:" + user[1]);

        await using var model = Db(clone.ConnectionString);
        var tables = BusinessTypes.ToDictionary(x => x, x => model.Model.FindEntityType(x)!);
        var rows = new Dictionary<Type, Dictionary<long, Dictionary<string, JsonElement>>>();
        foreach (var (type, meta) in tables)
        {
            rows[type] = [];
            foreach (var row in await Query($"""SELECT row_to_json(t)::text FROM "{meta.GetTableName()}" t"""))
            {
                var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>((string)row[0]!)!;
                rows[type][values["Id"].GetInt64()] = values;
            }
        }
        var crosswalk = (
            await Query(
                """
                SELECT "TargetType","TargetKey",string_agg("EntityType"||':'||"LegacyId", ',' ORDER BY "EntityType","LegacyId")
                FROM "DataMigrationRecords" WHERE "Source"='SS' GROUP BY 1,2
                """
            )
        ).ToDictionary(x => ((string)x[0]!, (string)x[1]!), x => (string)x[2]!);
        var labels = BusinessTypes.ToDictionary(x => x, _ => new Dictionary<long, string>());
        string? Known(Type type, long id) => labels[type].GetValueOrDefault(id);
        string Ref(Type type, JsonElement value) =>
            value.ValueKind == JsonValueKind.Null ? "null" : Known(type, value.GetInt64()) ?? $"?{type.Name}:{value}";
        string Text(JsonElement value) => value.ValueKind == JsonValueKind.Null ? "null" : value.ToString();
        string User(JsonElement value) =>
            value.ValueKind == JsonValueKind.Null ? "null" : userLabels.GetValueOrDefault(value.GetString()!, "?user");

        // 1. Crosswalked and baseline rows.
        foreach (var type in BusinessTypes)
        foreach (var id in rows[type].Keys)
        {
            if (crosswalk.TryGetValue((type.Name, id.ToString(CultureInfo.InvariantCulture)), out var owners))
                labels[type][id] = $"{type.Name}{{{owners}}}";
            else if (id <= clone.BaselineMax[type.Name])
                labels[type][id] = $"base:{type.Name}:{id}";
        }
        void Label(Type type, Func<Dictionary<string, JsonElement>, string?> structural)
        {
            foreach (var (id, row) in rows[type].Where(x => !labels[type].ContainsKey(x.Key)).OrderBy(x => x.Key))
            {
                var label = structural(row);
                if (label is null)
                    continue;
                var unique = label;
                for (var n = 2; labels[type].ContainsValue(unique); n++)
                    unique = $"{label}#{n}";
                labels[type][id] = unique;
            }
        }
        Label(typeof(CourtRoom), r => $"Room:{Text(r["Room"])}@{Text(r["LocationId"])}");
        Label(typeof(AssignmentDefinition), r => $"Definition:{Text(r["NormalizedName"])}@{Text(r["LocationId"])}");
        // Events/series are owned by their shift/entry/series rows.
        var eventOwners = new Dictionary<long, string>();
        foreach (var owner in new[] { typeof(ShiftEntry), typeof(AssignmentEntry) })
        foreach (var (id, row) in rows[owner])
            if (Known(owner, id) is { } label)
                eventOwners.TryAdd(row["EventId"].GetInt64(), label + "/Event");
        foreach (var (id, row) in rows[typeof(AssignmentSeries)])
            if (Known(typeof(AssignmentSeries), id) is null)
            {
                var series = rows[typeof(EventSeries)].GetValueOrDefault(row["EventSeriesId"].GetInt64());
                labels[typeof(AssignmentSeries)][id] =
                    $"Series:{Ref(typeof(AssignmentDefinition), row["AssignmentDefinitionId"])}"
                    + $"@{(series is null ? "?" : Text(series["StartAtUtc"]) + "/" + Text(series["RecurrenceRule"]))}";
            }
        var seriesOwners = rows[typeof(AssignmentSeries)]
            .GroupBy(x => x.Value["EventSeriesId"].GetInt64())
            .ToDictionary(x => x.Key, x => Known(typeof(AssignmentSeries), x.First().Key) + "/EventSeries");
        Label(
            typeof(EventSeries),
            r =>
                seriesOwners.GetValueOrDefault(r["Id"].GetInt64())
                ?? $"EventSeries:{Text(r["Title"])}@{Text(r["StartAtUtc"])}"
        );
        // Unowned events (receipt-less entries) fall back to content.
        foreach (var (id, row) in rows[typeof(Event)].Where(x => !labels[typeof(Event)].ContainsKey(x.Key)))
            if (eventOwners.TryGetValue(id, out var label))
                labels[typeof(Event)][id] = label;
        Label(
            typeof(Event),
            r =>
                $"Event:{Text(r["Title"])}@{Text(r["StartAtUtc"])}-{Text(r["EndAtUtc"])}:{Ref(typeof(EventSeries), r["EventSeriesId"])}"
        );
        Label(typeof(ShiftEntry), r => $"Shift:{Ref(typeof(Event), r["EventId"])}");
        Label(
            typeof(AssignmentEntry),
            r =>
                $"Entry:{Ref(typeof(AssignmentDefinition), r["AssignmentDefinitionId"])}:{Ref(typeof(Event), r["EventId"])}"
        );
        Label(typeof(ShiftEntryUser), r => $"{Ref(typeof(ShiftEntry), r["ShiftEntryId"])}/User:{User(r["UserId"])}");
        Label(
            typeof(ShiftAssignmentEntry),
            r =>
                $"Link:{Ref(typeof(ShiftEntry), r["ShiftEntryId"])}&{Ref(typeof(AssignmentEntry), r["AssignmentEntryId"])}"
        );
        Label(
            typeof(ShiftAssignmentEntryUser),
            r => $"{Ref(typeof(ShiftAssignmentEntry), r["ShiftAssignmentEntryId"])}/User:{User(r["UserId"])}"
        );

        string Format(IProperty property, JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Null)
                return "null";
            var fk = property
                .GetContainingForeignKeys()
                .FirstOrDefault(x => tables.ContainsKey(x.PrincipalEntityType.ClrType));
            if (fk is not null)
                return Ref(fk.PrincipalEntityType.ClrType, value);
            if (property.ClrType == typeof(Guid) || property.ClrType == typeof(Guid?))
                return userLabels.GetValueOrDefault(value.GetString()!, value.GetString()!);
            if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
            {
                var text = Iso(DateTimeOffset.Parse(value.GetString()!, CultureInfo.InvariantCulture).UtcDateTime);
                return startLabels.TryGetValue(text, out var run) ? "@" + run : text;
            }
            return value.ToString();
        }
        var result = new Dictionary<string, List<string>>();
        foreach (var (type, meta) in tables)
        {
            var properties = meta.GetProperties()
                .Where(x => !x.IsPrimaryKey() && !x.IsConcurrencyToken)
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .ToList();
            result[type.Name] = rows[type]
                .Select(x =>
                    labels[type].GetValueOrDefault(x.Key, $"?{type.Name}")
                    + " | "
                    + string.Join(
                        " | ",
                        properties.Select(p =>
                            p.Name
                            + "="
                            + (
                                p.Name == "UpdatedOn" && x.Value[p.GetColumnName()].ValueKind != JsonValueKind.Null
                                    ? "set"
                                    : Format(p, x.Value[p.GetColumnName()])
                            )
                        )
                    )
                )
                .Order(StringComparer.Ordinal)
                .ToList();
        }
        string TargetLabel(string type, string key)
        {
            var match = BusinessTypes.FirstOrDefault(x => x.Name == type);
            if (match is not null && long.TryParse(key, out var id))
                return labels[match].GetValueOrDefault(id, $"missing:{type}:{key}");
            if (type == "User")
                return userLabels.GetValueOrDefault(key, key);
            return type + ":" + key;
        }
        string AuditKey(string table, string pk)
        {
            var type = tables.FirstOrDefault(x => x.Value.GetTableName() == table).Key;
            if (type is not null && long.TryParse(pk, out var id))
                return labels[type].GetValueOrDefault(id, "missing:" + pk);
            return Regex.Replace(
                pk,
                "[0-9a-fA-F]{8}-[0-9a-fA-F-]{27}",
                m => userLabels.GetValueOrDefault(m.Value, "guid")
            );
        }
        string FormatValue(object? value) =>
            value switch
            {
                null => "null",
                DateTime time when startLabels.TryGetValue(Iso(time), out var label) => "@" + label,
                DateTime time => Iso(time),
                Guid id when runLabels.TryGetValue(id, out var label) => label,
                Guid id when userLabels.TryGetValue(id.ToString(), out var user) => user,
                Guid id => id.ToString(),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()!,
            };
        async Task<List<string>> Lines(string sql, Func<object?[], object?[]>? map = null) =>
            (await Query(sql))
                .Select(x => string.Join(" | ", (map?.Invoke(x) ?? x).Select(FormatValue)))
                .Order(StringComparer.Ordinal)
                .ToList();
        var schedulingRuns = """(SELECT "Id" FROM "DataMigrationRuns" WHERE "Job"='Scheduling' AND NOT "DryRun")""";
        result["runs"] = await Lines(
            """
            SELECT "Id","Status","ScannedCount","InsertedCount","UpdatedCount","UnchangedCount","SkippedCount",
                   "RejectedCount","FailedCount","HeartbeatOn" IS NOT NULL,"CompletedOn" IS NOT NULL,"MyTeamRunId"
            FROM "DataMigrationRuns" WHERE "Job"='Scheduling' AND NOT "DryRun"
            """
        );
        result["outcomes"] = await Lines(
            $"""
            SELECT "RunId","EntityType",
              CASE WHEN "EntityType" IN ('SchedulingValidation') THEN 'run' ELSE "LegacyId" END,"Outcome","Reason"
            FROM "DataMigrationOutcomes" WHERE "RunId" IN {schedulingRuns}
            """
        );
        result["failures"] = await Lines(
            $"""
            SELECT "RunId","EntityType","LegacyId","ErrorType","Watermark" FROM "DataMigrationFailures"
            WHERE "RunId" IN {schedulingRuns}
            """
        );
        result["workitems"] = await Lines(
            """
            SELECT "Stream","EntityType","LegacyId","Outcome","Reason","PendingRetry","Attempts","LastRunId",
                   "SourceChangedAtUtc"
            FROM "DataMigrationWorkItems" WHERE "Job"='Scheduling'
            """
        );
        result["checkpoints"] = await Lines(
            """
            SELECT "Stream","EntityType","LegacyId","IdKind","SourceChangedAtUtc","UpperLegacyId",
                   "UpperChangedAtUtc","LastRunId","HistoryStartDate"
            FROM "DataMigrationCheckpoints" WHERE "Job"='Scheduling'
            """
        );
        result["records"] = await Lines(
            """
            SELECT "EntityType","LegacyId","TargetType","TargetKey",
                   CASE WHEN "EntityType" IN ('Duty','DutySlot','DutyEntry','AssignmentSeries') THEN 'id-bound'
                        ELSE "PayloadHash" END,
                   "AfterHash" IS NULL,"SourceUpdatedAtUtc","CreatedByRunId","LastAppliedRunId","IsDeleted"
            FROM "DataMigrationRecords"
            WHERE "Source"='SS' AND "CreatedByRunId" IN (SELECT "Id" FROM "DataMigrationRuns" WHERE "Job"='Scheduling')
            """,
            x => [x[0], x[1], x[2], TargetLabel((string)x[2]!, (string)x[3]!), .. x[4..]]
        );
        result["linked"] = await Lines(
            $"""
            SELECT "EntityType","LegacyId","TargetType","TargetKey","LastAppliedRunId" FROM "DataMigrationRecords"
            WHERE "Source"='SS' AND "LastAppliedRunId" IN {schedulingRuns}
              AND NOT ("CreatedByRunId" IN {schedulingRuns})
            """,
            x => [x[0], x[1], x[2], TargetLabel((string)x[2]!, (string)x[3]!), x[4]]
        );
        result["mutations"] = await Lines(
            $"""
            SELECT r."EntityType",r."LegacyId",m."Operation",m."RunId",m."BeforeImage" IS NULL,m."Version" IS NULL,
                   m."AfterHash" IS NULL
            FROM "DataMigrationMutations" m JOIN "DataMigrationRecords" r ON r."Id"=m."DataMigrationRecordId"
            WHERE m."RunId" IN {schedulingRuns}
            """
        );
        result["audit"] = await Lines(
            $"""
            SELECT "TableName","EntityType","Action","ChangedColumns","ActorUserId","ActorName",
                (SELECT string_agg(k, ',' ORDER BY k) FROM jsonb_object_keys("NewValues") k),
                (SELECT string_agg(k, ',' ORDER BY k) FROM jsonb_object_keys("OldValues") k),
                "EntityPK","NewValues"->>'CreatedById' IS NULL,"NewValues"->>'UpdatedById' IS NULL
            FROM "AuditRecords"
            WHERE "TableName" IN ({string.Join(",", tables.Values.Select(x => $"'{x.GetTableName()}'"))})
            AND "Id" > {clone.AuditFloor}
            """,
            x => [.. x[..8], AuditKey((string)x[0]!, (string)x[8]!), .. x[9..]]
        );
        return result;
    }

    private static string Iso(DateTime time) =>
        DateTime
            .SpecifyKind(time, DateTimeKind.Utc)
            .ToString("yyyy-MM-ddTHH:mm:ss.ffffff", CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ helpers

    private static UnifiedDbContext Db(string connectionString) =>
        new(new DbContextOptionsBuilder<UnifiedDbContext>().UseNpgsql(connectionString).Options);

    private async Task<string> CreateDatabaseAsync(string name, string? template, CancellationToken ct)
    {
        var admin = new NpgsqlConnectionStringBuilder(Admin) { Pooling = false };
        await using (var connection = new NpgsqlConnection(admin.ConnectionString))
        {
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand(
                template is null
                    ? $"CREATE DATABASE \"{name}\""
                    : $"CREATE DATABASE \"{name}\" TEMPLATE \"{template}\"",
                connection
            )
            {
                CommandTimeout = 600,
            };
            await command.ExecuteNonQueryAsync(ct);
            databases.Add(name);
        }
        return new NpgsqlConnectionStringBuilder(admin.ConnectionString)
        {
            Database = name,
            Pooling = false,
            CommandTimeout = 600,
        }.ConnectionString;
    }

    private static async Task<long> CountAsync(string connectionString, string sql, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 600 };
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteAsync(
        string connectionString,
        string sql,
        CancellationToken ct,
        params NpgsqlParameter[] parameters
    )
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 600 };
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync(ct);
    }

    private sealed class Dispatcher(SsMigrationExecutor executor) : IMigrationJobDispatcher
    {
        public void Validate(string job, bool dryRun) => executor.ValidateRequest(job, dryRun);

        public void Enqueue(Guid runId) { }
    }

    private sealed class Authorization : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            IEnumerable<IAuthorizationRequirement> requirements
        ) => Task.FromResult(AuthorizationResult.Success());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName) =>
            Task.FromResult(AuthorizationResult.Success());
    }
}
