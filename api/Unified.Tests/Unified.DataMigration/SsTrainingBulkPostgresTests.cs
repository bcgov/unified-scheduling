using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Audit.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using Unified.Audit;
using Unified.Audit.Options;
using Unified.Authorization.Claims;
using Unified.DataMigration.FeatureFlags;
using Unified.DataMigration.Models;
using Unified.DataMigration.Options;
using Unified.DataMigration.Services;
using Unified.DataMigration.Services.Bulk;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Training;
using Unified.Db.Models.UserManagement;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Unified.Tests.DataMigration;

// Training PersistenceMode=Bulk proof against an isolated disposable PostgreSQL instance only.
// One frozen synthetic SS source and identical target clones (CREATE DATABASE ... TEMPLATE) with a
// genuinely completed MyTeam run each: PerRow on one clone, Bulk on another, compared after
// normalizing generated IDs through the crosswalk.
[CollectionDefinition(nameof(SsTrainingBulkPostgresCollection), DisableParallelization = true)]
public sealed class SsTrainingBulkPostgresCollection;

[Collection(nameof(SsTrainingBulkPostgresCollection))]
public sealed class SsTrainingBulkPostgresTests : IAsyncLifetime
{
    private const string Variable = "UNIFIED_MIGRATION_ISOLATED_TEST_CONNECTION_STRING";
    private const string SkipReason = "Set UNIFIED_MIGRATION_ISOLATED_TEST_CONNECTION_STRING for isolated PostgreSQL.";
    private const int Users = 12;
    private const int ExcusedUser = 11;
    private const int UnlinkedUser = 12;
    private const string BenchmarkVariable = "UNIFIED_MIGRATION_BULK_BENCHMARK_HISTORIES";
    private static string? Admin => Environment.GetEnvironmentVariable(Variable);
    public static bool IsConfigured => !string.IsNullOrWhiteSpace(Admin);
    public static bool IsBenchmarkConfigured =>
        IsConfigured && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(BenchmarkVariable));

    private static readonly string[] BusinessSections =
    [
        "trainings",
        "categories",
        "requirements",
        "usertrainings",
        "users",
        "records",
    ];

    private readonly string suffix = Guid.NewGuid().ToString("N")[..12];
    private readonly List<string> databases = [];
    private readonly Guid actor = Guid.NewGuid();
    private readonly IOptions<DataMigrationFeatureFlags> flags = Options.Create(
        new DataMigrationFeatureFlags { Enabled = true }
    );
    private readonly IConfiguration configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["JCInterface:ExpireRegions"] = "false",
                ["JCInterface:ExpireLocations"] = "false",
                ["JCInterface:ExpireCourtRooms"] = "false",
            }
        )
        .Build();
    private string sourceConnection = "";
    private string baseDatabase = "";

    public async ValueTask InitializeAsync()
    {
        if (!IsConfigured)
            return;
        var admin = new NpgsqlConnectionStringBuilder(Admin);
        if (
            admin.Host != "127.0.0.1"
            || !admin.Database!.StartsWith("unified_migration_isolated_", StringComparison.Ordinal)
        )
            throw new InvalidOperationException("An isolated loopback PostgreSQL instance is required.");
        var ct = TestContext.Current.CancellationToken;
        var source = await CreateDatabaseAsync($"migration_source_bulk_{suffix}", null, ct);
        sourceConnection = source;
        await using (var connection = new NpgsqlConnection(source))
        {
            await connection.OpenAsync(ct);
            await using var schema = new NpgsqlCommand(await File.ReadAllTextAsync(FindSourceSchema(), ct), connection)
            {
                CommandTimeout = 120,
            };
            await schema.ExecuteNonQueryAsync(ct);
        }
        await ExecuteAsync(source, SourceFixture(), ct);

        baseDatabase = $"migration_it_bulk_base_{suffix}";
        var target = await CreateDatabaseAsync(baseDatabase, null, ct);
        await using (var db = Db(target))
        {
            await db.Database.MigrateAsync(ct);
            // Production seeds the platform System user (PlatformSystemUserSeedData); background saves stamp it.
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
            db.TrainingProfileTypes.Add(
                new TrainingProfileType
                {
                    Code = "CARBINE_OPERATOR",
                    Name = "Carbine Operator",
                    Description = "Carbine Operator",
                    EffectiveDate = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    CreatedOn = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                }
            );
            db.Trainings.Add(
                new global::Unified.Db.Models.Training.Training
                {
                    Code = "Existing Course",
                    Description = "Pre-existing target course",
                    EffectiveDate = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    Order = 0,
                    CreatedOn = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                }
            );
            await db.SaveChangesAsync(ct);
        }
        NpgsqlConnection.ClearAllPools();
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
        foreach (var name in Enumerable.Reverse(databases))
        {
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsConfigured))]
    public async Task PerRowAndBulk_ProduceEquivalentTrainingState_AcrossAdverseRounds()
    {
        var ct = TestContext.Current.CancellationToken;
        // Audit.NET is a process-wide static (disabled for the test assembly); this collection runs
        // without parallelization, so enable the real pipeline here to compare PerRow vs Bulk audit rows.
        EnableAudit();
        try
        {
            await ParityAsync(ct);
        }
        finally
        {
            global::Audit.Core.Configuration.AuditDisabled = true;
        }
    }

    // Shared by the PerRow Audit.NET action and the Bulk writer so both honour the same exclusions.
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

    private async Task ParityAsync(CancellationToken ct)
    {
        var perRow = await CloneAsync("a", MigrationPersistenceMode.PerRow, ct);
        var bulk = await CloneAsync("b", MigrationPersistenceMode.Bulk, ct);

        var timings = new List<string>();
        for (var round = 1; round <= 4; round++)
        {
            if (round == 2)
            {
                await ExecuteAsync(sourceConnection, RoundTwoSource(), ct);
                foreach (var clone in new[] { perRow, bulk })
                    await EditTargetsAsync(clone, ct);
            }
            if (round == 4)
            {
                await ExecuteAsync(sourceConnection, RoundFourSource(), ct);
                // Even metadata-only migration edits bypass per-row audit in both modes.
                AuditOptions.ExcludedPropertyNames =
                [
                    .. AuditOptions.ExcludedPropertyNames,
                    "Notes",
                    "UpdatedOn",
                    "UpdatedById",
                ];
            }
            var perRowAuditFloor = await CountAsync(
                perRow,
                """SELECT COALESCE(MAX("Id"),0) FROM "AuditRecords" """,
                ct
            );
            var bulkAuditFloor = await CountAsync(bulk, """SELECT COALESCE(MAX("Id"),0) FROM "AuditRecords" """, ct);
            var watch = Stopwatch.StartNew();
            var perRowRun = await RunAsync(perRow, "Training", ct);
            var perRowElapsed = watch.Elapsed;
            watch.Restart();
            var bulkRun = await RunAsync(bulk, "Training", ct);
            timings.Add(
                $"round {round}: PerRow {perRowElapsed.TotalMilliseconds:F0} ms, Bulk {watch.ElapsedMilliseconds} ms"
            );
            await AssertRunAuditAsync(perRow, perRowRun, perRowAuditFloor, ct);
            await AssertRunAuditAsync(bulk, bulkRun, bulkAuditFloor, ct);
            if (round == 4)
            {
                foreach (
                    var (clone, id, floor) in new[]
                    {
                        (perRow, perRowRun, perRowAuditFloor),
                        (bulk, bulkRun, bulkAuditFloor),
                    }
                )
                {
                    await using var terminal = clone.Db();
                    await Assert.ThrowsAsync<InvalidOperationException>(() =>
                        Executor(terminal, clone.Options).ExecuteAcquiredAsync(id, ct)
                    );
                    await AssertRunAuditAsync(clone, id, floor, ct);
                }
            }
            if (round < 4)
            {
                await AssertCleanAsync(perRow, perRowRun, ct);
                await AssertCleanAsync(bulk, bulkRun, ct);
            }
            else
            {
                // Oversized mapped notes fail only their own row (PerRow: failed save rolled back to the
                // row savepoint), never the whole Bulk chunk.
                foreach (var (clone, id) in new[] { (perRow, perRowRun), (bulk, bulkRun) })
                {
                    await using var check = clone.Db();
                    var failed = await check
                        .DataMigrationOutcomes.AsNoTracking()
                        .Where(x => x.RunId == id && x.Outcome == "Failed")
                        .Select(x => x.LegacyId + ":" + x.Reason)
                        .ToListAsync(ct);
                    Assert.Equal(
                        [
                            "1073:TargetWriteFailed:DbUpdateException",
                            "4001:TargetWriteFailed:DbUpdateException",
                            "4005:TargetWriteFailed:DbUpdateException",
                        ],
                        failed.Order(StringComparer.Ordinal).ToArray()
                    );
                    // PostgreSQL accepts over-length varchar values whose excess is all spaces (stored
                    // truncated), so these migrate in both modes; a non-space in the excess (4005) fails.
                    Assert.Equal(
                        2,
                        await CountAsync(
                            clone,
                            """
                            SELECT COUNT(*) FROM "UserTrainings" u JOIN "DataMigrationRecords" r
                              ON r."TargetKey"=u."Id"::text AND r."EntityType"='SheriffTraining'
                              AND r."LegacyId" IN ('4003','4004')
                            WHERE char_length(u."Notes")=2000
                            """,
                            ct
                        )
                    );
                    // The interceptor path is live in both modes: migrated rows carry the System actor.
                    Assert.True(
                        await CountAsync(
                            clone,
                            $"""SELECT COUNT(*) FROM "UserTrainings" WHERE "CreatedById"='{User.SystemUser}'""",
                            ct
                        ) > 0
                    );
                    Assert.True(
                        await CountAsync(
                            clone,
                            $"""SELECT COUNT(*) FROM "UserTrainings" WHERE "UpdatedById"='{User.SystemUser}'""",
                            ct
                        ) > 0
                    );
                    // 1001 astral characters (2002 UTF-16 units) fit varchar(2000) and migrate in both modes.
                    Assert.Equal(
                        1,
                        await CountAsync(
                            clone,
                            """
                            SELECT COUNT(*) FROM "UserTrainings" u JOIN "DataMigrationRecords" r
                              ON r."TargetKey"=u."Id"::text AND r."EntityType"='SheriffTraining' AND r."LegacyId"='4002'
                            WHERE char_length(u."Notes")=1001
                            """,
                            ct
                        )
                    );
                    Assert.Equal(
                        0, // Migration bypass also suppresses metadata-only Modified audits.
                        await CountAsync(
                            clone,
                            $"""
                            SELECT COUNT(*) FROM "AuditRecords" a
                            WHERE a."Id" > {clone.AuditFloor} AND a."TableName"='UserTrainings' AND a."Action"='Modified'
                              AND a."EntityPK" IN (SELECT "TargetKey" FROM "DataMigrationRecords" WHERE "LegacyId"='1074')
                              AND a."OldValues" IS NULL AND a."NewValues" IS NULL AND a."ChangedColumns" IS NULL
                              AND a."ActorName"='System'
                            """,
                            ct
                        )
                    );
                }
            }

            var expected = await SnapshotAsync(perRow, false, ct);
            var actual = await SnapshotAsync(bulk, false, ct);
            AssertEquivalent(expected, actual, expected.Keys, $"round {round}");
            timings.Add(
                $"round {round} compared lines: " + string.Join(", ", expected.Select(x => $"{x.Key}={x.Value.Count}"))
            );

            await using var context = bulk.Db();
            var run = await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == bulkRun, ct);
            Assert.Null(run.RollbackJournalKeyId); // Bulk captures no rollback journal.
            Assert.True(run.CommittedBatchCount > 3, "Bulk must commit several chunks.");
            Assert.NotNull(run.HeartbeatOn);
            await AssertNoDuplicatesOrOrphansAsync(bulk, ct);
            if (round == 1)
            {
                var reasons = await ReasonsAsync(bulk, bulkRun, ct);
                foreach (
                    var reason in new[]
                    {
                        "InvalidCourseCode",
                        "CourseCodeShortened",
                        "DuplicateCourseCode",
                        "UnsupportedScopedCourse",
                        "UnapprovedTrainingCategory",
                        "CarbineCourseNotMandatory",
                        "InvalidAdvanceNotice",
                        "NegativeTrainingPeriod",
                        "CourseDescriptionTooLong",
                        "MissingTraining",
                        "MissingParent",
                        "MissingSourceUser",
                        "ExcusedSheriff",
                        "DeletedHistory",
                        "ReversedDates",
                        "InvalidTimezone",
                        "TargetTrainingTimezoneUnavailable",
                        "UnexpectedNonOtherComment",
                        "UnifiedMandatoryPolicyReplacesFutureRules",
                    }
                )
                    Assert.Contains(reason, reasons);
                // Training has no history date limit: 2009 ends just before the 2020-01-01 Vancouver boundary
                // and still migrates in both modes (parity is asserted above).
                Assert.DoesNotContain("BeforeHistoryBoundary", reasons);
                foreach (var (clone, id) in new[] { (perRow, perRowRun), (bulk, bulkRun) })
                    Assert.StartsWith("Migrated", await OutcomeAsync(clone, id, "SheriffTraining", "2009", ct));
                // Rejected parent courses leave no orphan children.
                Assert.Equal(
                    0,
                    await CountAsync(
                        bulk,
                        """
                        SELECT COUNT(*) FROM "DataMigrationOutcomes" o
                        JOIN "DataMigrationRecords" r ON r."EntityType"='SheriffTraining' AND r."LegacyId"=o."LegacyId"
                        WHERE o."EntityType"='SheriffTraining' AND o."Outcome"='Rejected'
                        """,
                        ct
                    )
                );
            }
            if (round == 2)
            {
                var reasons = await ReasonsAsync(bulk, bulkRun, ct);
                foreach (
                    var reason in new[]
                    {
                        "OwnedTargetDrift",
                        "LateOlderHistoryAppended",
                        "PreviouslyMigratedNowExcludedRetained",
                        "UnchangedMappedSource",
                        "ExistingCrosswalk",
                    }
                )
                    Assert.Contains(reason, reasons);
                Assert.True(run.UpdatedCount > 0);
            }
            if (round == 3)
            {
                Assert.Equal(0, run.InsertedCount);
                Assert.Equal(0, run.UpdatedCount);
            }
        }
        foreach (var line in timings)
            TestContext.Current.TestOutputHelper?.WriteLine(line);
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsConfigured))]
    public async Task Bulk_RefusesConcurrentAndTerminalRuns_ResumesAfterCrashAndAbortWithoutDuplicates()
    {
        var ct = TestContext.Current.CancellationToken;
        var reference = await CloneAsync("ref", MigrationPersistenceMode.PerRow, ct);
        var referenceRun = await RunAsync(reference, "Training", ct);
        await AssertCleanAsync(reference, referenceRun, ct);
        var expected = await SnapshotAsync(reference, true, ct);
        await using (var context = reference.Db())
        {
            var refused = await new SsBulkUndo(context).UndoAsync(referenceRun, ct);
            Assert.Null(refused.ReceiptRunId);
            // Journaling is disabled in this harness, so the PerRow run carries no journal key; the
            // undo must still refuse it because its trigger evidence bound PerRow, not Bulk.
            Assert.Contains("RunNotBulkMode", refused.Blockers);
            Assert.Equal(
                "Failed",
                (await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == referenceRun, ct)).Status
            );
        }

        // Crash inside a chunk transaction after earlier chunks committed; a concurrent execution
        // of the same run and a second trigger are refused while it is running.
        var crash = await CloneAsync("crash", MigrationPersistenceMode.Bulk, ct);
        await using (var context = crash.Db())
        {
            var first = await Operations(context, crash.Options).TriggerAsync("SS", "Training", false, actor, ct);
            await using var second = crash.Db();
            var busy = await Assert.ThrowsAsync<MigrationOperationException>(() =>
                Operations(second, crash.Options).TriggerAsync("SS", "Training", false, actor, ct)
            );
            Assert.Equal("JobBusyPausedOrLineageNotReady", busy.Code);
            await using var worker = crash.Db();
            var probed = false;
            var writer = new SsTrainingBulkWriter(worker)
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
                        // Undo during an active chunk is refused before taking table locks (no lock wait or
                        // deadlock with the chunk transaction).
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
            Assert.True(await CountAsync(crash, "SELECT COUNT(*) FROM \"UserTrainings\"", ct) > 0);

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
        var resumed = await RunAsync(crash, "Training", ct);
        await AssertCleanAsync(crash, resumed, ct);
        await AssertNoDuplicatesOrOrphansAsync(crash, ct);
        AssertEquivalent(expected, await SnapshotAsync(crash, true, ct), BusinessSections, "crash resume");
        var rerun = await RunAsync(crash, "Training", ct);
        await using (var context = crash.Db())
        {
            var run = await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == rerun, ct);
            Assert.Equal("Failed", run.Status); // Expected source rejections persist across reruns.
            Assert.Equal(0, run.InsertedCount);
            Assert.Equal(0, run.UpdatedCount);
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
        AssertEquivalent(expected, await SnapshotAsync(crash, true, ct), BusinessSections, "crash rerun");

        // Abort control set while a chunk is in flight: that chunk commits, the next chunk is fenced.
        var abort = await CloneAsync("abort", MigrationPersistenceMode.Bulk, ct);
        var aborted = await RunAsync(
            abort,
            "Training",
            ct,
            hook: async (index, token) =>
            {
                if (index == 1)
                    await ExecuteAsync(
                        abort.ConnectionString,
                        """
                        INSERT INTO "DataMigrationControls" ("Source","IsPaused","AbortRequestedOn","ChangedOn","ChangedBy")
                        VALUES ('SS:Training', false, now(), now(), 'test')
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
        await ExecuteAsync(abort.ConnectionString, "DELETE FROM \"DataMigrationControls\"", ct);
        var afterAbort = await RunAsync(abort, "Training", ct);
        await AssertCleanAsync(abort, afterAbort, ct);
        await AssertNoDuplicatesOrOrphansAsync(abort, ct);
        AssertEquivalent(expected, await SnapshotAsync(abort, true, ct), BusinessSections, "abort resume");

        // Token cancellation between chunks stops cleanly with a Canceled failure.
        var cancel = await CloneAsync("cancel", MigrationPersistenceMode.Bulk, ct);
        using var source = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await using (var context = cancel.Db())
        {
            var accepted = await Operations(context, cancel.Options).TriggerAsync("SS", "Training", false, actor, ct);
            await using var worker = cancel.Db();
            var writer = new SsTrainingBulkWriter(worker)
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
        var afterCancel = await RunAsync(cancel, "Training", ct);
        await AssertCleanAsync(cancel, afterCancel, ct);
        AssertEquivalent(expected, await SnapshotAsync(cancel, true, ct), BusinessSections, "cancel resume");
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsConfigured))]
    public async Task Bulk_DryRunPersistsNothing_ModeIsBoundAtTrigger_UndoRestoresBaseline()
    {
        var ct = TestContext.Current.CancellationToken;
        var clone = await CloneAsync("undo", MigrationPersistenceMode.Bulk, ct);
        var baseline = await SnapshotAsync(clone, true, ct);

        // Bulk + DryRun uses the existing simulation path and persists no business rows.
        var dryMyTeam = await RunAsync(clone, "MyTeam", ct, dryRun: true);
        await AssertRunAsync(clone, dryMyTeam, "Completed", ct);
        var dryTraining = await RunAsync(clone, "Training", ct, dryRun: true);
        await using (var context = clone.Db())
        {
            var run = await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == dryTraining, ct);
            Assert.True(run.DryRun);
            Assert.True(run.ScannedCount > 0);
        }
        AssertEquivalent(baseline, await SnapshotAsync(clone, true, ct), BusinessSections, "bulk dry run");

        // The persistence mode is bound into the audited trigger; a later configuration change refuses the run.
        await using (var context = clone.Db())
        {
            var accepted = await Operations(context, clone.Options).TriggerAsync("SS", "Training", false, actor, ct);
            clone.Options.Sources.SS.Jobs.Training.PersistenceMode = MigrationPersistenceMode.PerRow;
            await using var worker = clone.Db();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Executor(worker, clone.Options).ExecuteAcquiredAsync(accepted.RunId, ct)
            );
            clone.Options.Sources.SS.Jobs.Training.PersistenceMode = MigrationPersistenceMode.Bulk;
            await using var check = clone.Db();
            var run = await check.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == accepted.RunId, ct);
            Assert.Equal("Failed", run.Status);
            Assert.Equal(0, run.InsertedCount);
        }
        AssertEquivalent(baseline, await SnapshotAsync(clone, true, ct), BusinessSections, "mode change refused");

        var applied = await RunAsync(clone, "Training", ct);
        await AssertCleanAsync(clone, applied, ct);
        Assert.True(await CountAsync(clone, "SELECT COUNT(*) FROM \"UserTrainings\"", ct) > 0);
        Assert.True(
            await CountAsync(clone, "SELECT COUNT(*) FROM \"Users\" WHERE \"TrainingProfileId\" IS NOT NULL", ct) > 0
        );

        // Crosswalk-scoped undo returns business rows and Training crosswalks to the post-MyTeam baseline.
        await using (var context = clone.Db())
        {
            var result = await new SsBulkUndo(context).UndoAsync(applied, ct);
            Assert.Empty(result.Blockers);
            Assert.NotNull(result.ReceiptRunId);
        }
        var undone = await SnapshotAsync(clone, true, ct);
        AssertEquivalent(baseline, undone, BusinessSections, "bulk undo");
        await using (var context = clone.Db())
        {
            Assert.Equal(
                "RolledBack",
                (await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == applied, ct)).Status
            );
            var again = await new SsBulkUndo(context).UndoAsync(applied, ct);
            Assert.Contains("RunNotApplied", again.Blockers);
        }

        // The undone run can be re-applied cleanly.
        var reapplied = await RunAsync(clone, "Training", ct);
        await AssertCleanAsync(clone, reapplied, ct);
        await AssertNoDuplicatesOrOrphansAsync(clone, ct);
        await using (var context = clone.Db())
        {
            var run = await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == reapplied, ct);
            Assert.True(run.InsertedCount > 0);
        }

        // Receipt-less rows the run created (categories, carbine requirements) are xmin-checked: a later
        // edit to either blocks undo and nothing changes.
        var started = $"(SELECT \"StartedOn\" FROM \"DataMigrationRuns\" WHERE \"Id\"='{reapplied}')";
        Assert.True(
            await CountAsync(clone, $"SELECT COUNT(*) FROM \"TrainingCategories\" WHERE \"CreatedOn\"={started}", ct)
                > 0
        );
        Assert.True(
            await CountAsync(
                clone,
                $"SELECT COUNT(*) FROM \"TrainingMandatoryTrainingProfiles\" WHERE \"CreatedOn\"={started}",
                ct
            ) > 0
        );
        await ExecuteAsync(
            clone.ConnectionString,
            $"""
            UPDATE "TrainingCategories" SET "Description"='edited in Unified', "UpdatedOn"=now()
            WHERE "CreatedOn"={started};
            UPDATE "TrainingMandatoryTrainingProfiles" SET "UpdatedOn"=now() WHERE "CreatedOn"={started};
            """,
            ct
        );
        var edited = await SnapshotAsync(clone, true, ct);
        await using (var context = clone.Db())
        {
            var blocked = await new SsBulkUndo(context).UndoAsync(reapplied, ct);
            Assert.Null(blocked.ReceiptRunId);
            Assert.Contains("CreatedCategoryChanged", blocked.Blockers);
            Assert.Contains("CreatedCourseRequirementsChanged", blocked.Blockers);
        }
        AssertEquivalent(edited, await SnapshotAsync(clone, true, ct), BusinessSections, "blocked undo");
    }

    // Training migrates SheriffTraining with no history date limit, once, in both modes; reruns are no-ops for
    // unchanged rows and never overwrite a target edited in Unified (incl. the 2026-11-01 time zone correction).
    // MyTeam/Leave keep the HistoryStartDate boundary.
    [Fact(Skip = SkipReason, SkipUnless = nameof(IsConfigured))]
    public async Task Training_NoHistoryLimit_RerunIsNoOp_DriftedAndTimezoneAdjustedTargetsAreSkipped()
    {
        var ct = TestContext.Current.CancellationToken;
        await ExecuteAsync(
            sourceConnection,
            $"""
            INSERT INTO "SheriffTraining" ("Id","SheriffId","TrainingTypeId","StartDate","EndDate","ExpiryDate",
                "TrainingCertificationExpiry","FirstNotice","Comment","Note","Timezone","CreatedOn") VALUES
            (5001,'{U(
                2
            )}',100,'2009-03-10T17:00Z','2009-03-11T01:00Z',NULL,NULL,false,NULL,'pre-2020 history',NULL,'2024-01-01Z'),
            (5002,'{U(
                3
            )}',101,'2026-11-15T17:00Z','2026-11-16T01:00Z',NULL,NULL,false,NULL,'after tz change',NULL,'2024-01-01Z');
            INSERT INTO "SheriffLeave" ("Id","CreatedOn","LeaveTypeId","StartDate","EndDate","SheriffId")
            VALUES (9001,'2024-01-01',NULL,'2009-03-01','2009-03-02','{U(1)}');
            """,
            ct
        );
        var clones = new[]
        {
            await CloneAsync("nolimit_a", MigrationPersistenceMode.PerRow, ct),
            await CloneAsync("nolimit_b", MigrationPersistenceMode.Bulk, ct),
        };
        string[] business = ["trainings", "categories", "requirements", "usertrainings", "users"];

        foreach (var clone in clones)
        {
            // The MyTeam (leave) history boundary is unchanged.
            await using (var context = clone.Db())
            {
                var myTeam = await context
                    .DataMigrationRuns.AsNoTracking()
                    .Where(x => x.Job == "MyTeam" && !x.DryRun)
                    .Select(x => x.Id)
                    .SingleAsync(ct);
                Assert.Equal(
                    "Excluded:BeforeHistoryBoundary",
                    await OutcomeAsync(clone, myTeam, "SheriffLeave", "9001", ct)
                );
            }
        }

        // Dry run (PerRow and Bulk share the simulation path): pre-2020 rows are proposed, nothing persists,
        // and a repeated dry run reports the same counts.
        var dry = clones[0];
        var beforeDry = await SnapshotAsync(dry, true, ct);
        await AssertRunAsync(dry, await RunAsync(dry, "MyTeam", ct, dryRun: true), "Completed", ct);
        var dryRuns = new List<global::Unified.Db.Models.DataMigration.DataMigrationRun>();
        for (var i = 0; i < 2; i++)
        {
            var id = await RunAsync(dry, "Training", ct, dryRun: true);
            await using var context = dry.Db();
            dryRuns.Add(await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == id, ct));
            foreach (var legacy in new[] { "2009", "5001" })
                Assert.StartsWith("Migrated", await OutcomeAsync(dry, id, "SheriffTraining", legacy, ct));
        }
        Assert.Equal(
            (dryRuns[0].ScannedCount, dryRuns[0].InsertedCount, dryRuns[0].SkippedCount, dryRuns[0].RejectedCount),
            (dryRuns[1].ScannedCount, dryRuns[1].InsertedCount, dryRuns[1].SkippedCount, dryRuns[1].RejectedCount)
        );
        AssertEquivalent(beforeDry, await SnapshotAsync(dry, true, ct), business, "dry run persists nothing");

        // First apply: pre-2020 histories are created exactly once.
        var first = new Guid[clones.Length];
        for (var i = 0; i < clones.Length; i++)
        {
            first[i] = await RunAsync(clones[i], "Training", ct);
            await AssertCleanAsync(clones[i], first[i], ct);
            Assert.DoesNotContain("BeforeHistoryBoundary", await ReasonsAsync(clones[i], first[i], ct));
            foreach (var legacy in new[] { "2009", "5001", "5002" })
                Assert.StartsWith("Migrated", await OutcomeAsync(clones[i], first[i], "SheriffTraining", legacy, ct));
            await AssertNoDuplicatesOrOrphansAsync(clones[i], ct);
        }
        AssertEquivalent(
            await SnapshotAsync(clones[0], false, ct),
            await SnapshotAsync(clones[1], false, ct),
            business,
            "no-limit first apply"
        );

        // (a) A second run is a no-op.
        for (var i = 0; i < clones.Length; i++)
        {
            var before = await SnapshotAsync(clones[i], true, ct);
            var second = await RunAsync(clones[i], "Training", ct);
            await AssertCleanAsync(clones[i], second, ct);
            await using (var context = clones[i].Db())
            {
                var run = await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == second, ct);
                Assert.Equal((0, 0), (run.InsertedCount, run.UpdatedCount));
            }
            foreach (var legacy in new[] { "2009", "5001", "5002" })
                Assert.Equal(
                    "Migrated:ExistingCrosswalk",
                    await OutcomeAsync(clones[i], second, "SheriffTraining", legacy, ct)
                );
            await AssertNoDuplicatesOrOrphansAsync(clones[i], ct);
            AssertEquivalent(before, await SnapshotAsync(clones[i], true, ct), business, "second run no-op");
        }

        // (c) Post-2026-11-01 times adjusted in Unified (time zone change) and (b) a target edited in Unified.
        foreach (var clone in clones)
            await ExecuteAsync(
                clone.ConnectionString,
                """
                UPDATE "UserTrainings" SET "AwardedOn"="AwardedOn" + interval '1 hour',
                    "EndingOn"="EndingOn" + interval '1 hour'
                WHERE "Id"::text IN (SELECT "TargetKey" FROM "DataMigrationRecords"
                    WHERE "EntityType"='SheriffTraining' AND "LegacyId"='5002');
                UPDATE "UserTrainings" SET "Notes"='edited in Unified'
                WHERE "Id"::text IN (SELECT "TargetKey" FROM "DataMigrationRecords"
                    WHERE "EntityType"='SheriffTraining' AND "LegacyId" IN ('5001','1041'));
                """,
                ct
            );
        var edited = new List<Dictionary<string, string>>();
        foreach (var clone in clones)
        {
            var values = new Dictionary<string, string>();
            foreach (var legacy in new[] { "5001", "5002", "1041" })
                values[legacy] = await TargetHistoryAsync(clone, legacy, ct);
            edited.Add(values);
        }

        // Unchanged source: the adjusted/edited targets are left alone.
        for (var i = 0; i < clones.Length; i++)
        {
            var third = await RunAsync(clones[i], "Training", ct);
            await AssertCleanAsync(clones[i], third, ct);
            foreach (var (legacy, value) in edited[i])
            {
                Assert.Equal(
                    "Migrated:ExistingCrosswalk",
                    await OutcomeAsync(clones[i], third, "SheriffTraining", legacy, ct)
                );
                Assert.Equal(value, await TargetHistoryAsync(clones[i], legacy, ct));
            }
        }

        // Changed source over drifted targets: skipped (Excluded/OwnedTargetDrift), never overwritten, on
        // every rerun; nothing is rejected for it.
        await ExecuteAsync(
            sourceConnection,
            """
            UPDATE "SheriffTraining" SET "Note"='changed in SS', "UpdatedOn"='2026-11-20Z'
            WHERE "Id" IN (5001,5002,1041);
            """,
            ct
        );
        for (var round = 0; round < 2; round++)
        for (var i = 0; i < clones.Length; i++)
        {
            var run = await RunAsync(clones[i], "Training", ct);
            await AssertCleanAsync(clones[i], run, ct);
            await using (var context = clones[i].Db())
                Assert.Equal(
                    0,
                    (await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == run, ct)).UpdatedCount
                );
            foreach (var (legacy, value) in edited[i])
            {
                Assert.Equal(
                    "Excluded:OwnedTargetDrift",
                    await OutcomeAsync(clones[i], run, "SheriffTraining", legacy, ct)
                );
                Assert.Equal(value, await TargetHistoryAsync(clones[i], legacy, ct));
            }
            await AssertNoDuplicatesOrOrphansAsync(clones[i], ct);
        }
    }

    // DataMigration:Sources:SS:Jobs:Training:IncludeExcused. Off (default): excused users' training is
    // Excluded/ExcusedSheriff. On: migrated like any other user's in PerRow, Bulk and dry-run, once; an excused user
    // without a MyTeam user crosswalk rejects with MissingParent. Turning it off again retains migrated rows.
    [Fact(Skip = SkipReason, SkipUnless = nameof(IsConfigured))]
    public async Task Training_IncludeExcused_OffExcludes_OnMigratesIdempotently()
    {
        var ct = TestContext.Current.CancellationToken;
        await ExecuteAsync(
            sourceConnection,
            $"""
            UPDATE "User" SET "Excused"=true WHERE "Id"='{U(UnlinkedUser)}';
            INSERT INTO "SheriffTraining" ("Id","SheriffId","TrainingTypeId","StartDate","EndDate","ExpiryDate",
                "TrainingCertificationExpiry","FirstNotice","Comment","Note","Timezone","CreatedOn") VALUES
            (5101,'{U(
                ExcusedUser
            )}',100,'2009-05-10T17:00Z','2009-05-11T01:00Z',NULL,NULL,false,NULL,'excused pre-2020',NULL,'2024-01-01Z');
            """,
            ct
        );
        var clones = new[]
        {
            await CloneAsync("excused_a", MigrationPersistenceMode.PerRow, ct),
            await CloneAsync("excused_b", MigrationPersistenceMode.Bulk, ct),
        };
        string[] business = ["trainings", "categories", "requirements", "usertrainings", "users"];
        string[] excused = ["2007", "5101"];

        // Flag off (default): ExcusedSheriff, nothing created.
        foreach (var clone in clones)
        {
            Assert.False(clone.Options.Sources.SS.Jobs.Training.IncludeExcused);
            var off = await RunAsync(clone, "Training", ct);
            await AssertCleanAsync(clone, off, ct);
            foreach (var legacy in excused.Append("2012"))
                Assert.Equal("Excluded:ExcusedSheriff", await OutcomeAsync(clone, off, "SheriffTraining", legacy, ct));
            Assert.Equal(
                0,
                await CountAsync(
                    clone,
                    """SELECT COUNT(*) FROM "DataMigrationRecords" WHERE "EntityType"='SheriffTraining' AND "LegacyId" IN ('2007','5101','2012')""",
                    ct
                )
            );
            clone.Options.Sources.SS.Jobs.Training.IncludeExcused = true;
        }

        // Dry run with the flag on proposes the excused rows and persists nothing.
        var dry = clones[0];
        var beforeDry = await SnapshotAsync(dry, true, ct);
        await AssertRunAsync(dry, await RunAsync(dry, "MyTeam", ct, dryRun: true), "Completed", ct);
        var dryRun = await RunAsync(dry, "Training", ct, dryRun: true);
        foreach (var legacy in excused)
            Assert.StartsWith("Migrated", await OutcomeAsync(dry, dryRun, "SheriffTraining", legacy, ct));
        // The simulated MyTeam parent recreates the deleted user crosswalk, so 2012 is also proposed in dry run.
        Assert.StartsWith("Migrated", await OutcomeAsync(dry, dryRun, "SheriffTraining", "2012", ct));
        AssertEquivalent(beforeDry, await SnapshotAsync(dry, true, ct), business, "excused dry run persists nothing");

        // Flag on: the previously excluded rows are reconsidered and migrated once; missing user parent rejects.
        for (var i = 0; i < clones.Length; i++)
        {
            var on = await RunAsync(clones[i], "Training", ct);
            await AssertCleanAsync(clones[i], on, ct);
            Assert.DoesNotContain("ExcusedSheriff", await ReasonsAsync(clones[i], on, ct));
            foreach (var legacy in excused)
                Assert.StartsWith("Migrated", await OutcomeAsync(clones[i], on, "SheriffTraining", legacy, ct));
            Assert.Equal("Rejected:MissingParent", await OutcomeAsync(clones[i], on, "SheriffTraining", "2012", ct));
            await AssertNoDuplicatesOrOrphansAsync(clones[i], ct);
        }
        AssertEquivalent(
            await SnapshotAsync(clones[0], false, ct),
            await SnapshotAsync(clones[1], false, ct),
            business,
            "include-excused apply"
        );

        // Rerun with the flag on is a no-op.
        for (var i = 0; i < clones.Length; i++)
        {
            var before = await SnapshotAsync(clones[i], true, ct);
            var rerun = await RunAsync(clones[i], "Training", ct);
            await AssertCleanAsync(clones[i], rerun, ct);
            await using (var context = clones[i].Db())
            {
                var run = await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == rerun, ct);
                Assert.Equal((0, 0), (run.InsertedCount, run.UpdatedCount));
            }
            foreach (var legacy in excused)
                Assert.Equal(
                    "Migrated:ExistingCrosswalk",
                    await OutcomeAsync(clones[i], rerun, "SheriffTraining", legacy, ct)
                );
            AssertEquivalent(before, await SnapshotAsync(clones[i], true, ct), business, "include-excused rerun no-op");
        }

        // Flag off again: already migrated excused rows are retained, never deleted.
        foreach (var clone in clones)
        {
            clone.Options.Sources.SS.Jobs.Training.IncludeExcused = false;
            var before = await SnapshotAsync(clone, true, ct);
            var off = await RunAsync(clone, "Training", ct);
            await AssertCleanAsync(clone, off, ct);
            foreach (var legacy in excused)
                Assert.Equal(
                    "Excluded:PreviouslyMigratedNowExcludedRetained",
                    await OutcomeAsync(clone, off, "SheriffTraining", legacy, ct)
                );
            AssertEquivalent(before, await SnapshotAsync(clone, true, ct), business, "excused retained");
        }
    }

    // Benchmark (opt-in): UNIFIED_MIGRATION_BULK_BENCHMARK_HISTORIES=<n> extra synthetic histories.
    [Fact(
        Skip = "Set " + BenchmarkVariable + " to benchmark PerRow vs Bulk.",
        SkipUnless = nameof(IsBenchmarkConfigured)
    )]
    public async Task Benchmark_PerRowVsBulk()
    {
        var ct = TestContext.Current.CancellationToken;
        var count = int.Parse(Environment.GetEnvironmentVariable(BenchmarkVariable)!, CultureInfo.InvariantCulture);
        var users = Math.Max(1, count / 20);
        await ExecuteAsync(
            sourceConnection,
            $"""
            INSERT INTO "User" ("Id","CreatedOn","IdirId","Discriminator","IdirName","IsEnabled","FirstName",
                "LastName","Email","Excused")
            SELECT ('20000000-0000-0000-0000-' || lpad(n::text,12,'0'))::uuid, '2024-01-01Z',
                ('30000000-0000-0000-0000-' || lpad(n::text,12,'0'))::uuid, 'Sheriff', 'bench' || n, true,
                'Bench', 'User' || n, 'bench' || n || '@example.invalid', false
            FROM generate_series(1,{users}) n;
            INSERT INTO "SheriffTraining" ("Id","SheriffId","TrainingTypeId","StartDate","EndDate","ExpiryDate",
                "TrainingCertificationExpiry","FirstNotice","Comment","Note","Timezone","CreatedOn")
            SELECT 100000 + n, ('20000000-0000-0000-0000-' || lpad((1 + n % {users})::text,12,'0'))::uuid,
                (ARRAY[100,101,102,104,113,115])[1 + n % 6],
                timestamptz '2020-02-01Z' + (n % 1800) * interval '1 day',
                timestamptz '2020-02-01Z' + (n % 1800) * interval '1 day' + interval '8 hours',
                CASE WHEN n % 50 = 0 THEN timestamptz '2025-01-01Z' END,
                timestamptz '2023-02-01Z' + (n % 1800) * interval '1 day', n % 3 = 0, NULL,
                'bench note ' || n, CASE WHEN n % 7 = 0 THEN 'America/Vancouver' END, '2024-01-01Z'
            FROM generate_series(1,{count}) n;
            """,
            ct
        );
        var perRow = await CloneAsync("bench_a", MigrationPersistenceMode.PerRow, ct);
        var bulk = await CloneAsync("bench_b", MigrationPersistenceMode.Bulk, ct, chunk: 2000);
        var watch = Stopwatch.StartNew();
        var perRowRun = await RunAsync(perRow, "Training", ct);
        var perRowElapsed = watch.Elapsed;
        watch.Restart();
        var bulkRun = await RunAsync(bulk, "Training", ct);
        var bulkElapsed = watch.Elapsed;
        await AssertCleanAsync(perRow, perRowRun, ct);
        await AssertCleanAsync(bulk, bulkRun, ct);
        await using var context = bulk.Db();
        var scanned = (
            await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == bulkRun, ct)
        ).ScannedCount;
        var output = TestContext.Current.TestOutputHelper;
        output?.WriteLine($"Scanned source rows: {scanned}");
        output?.WriteLine(
            $"PerRow: {perRowElapsed.TotalSeconds:F1} s ({scanned / perRowElapsed.TotalSeconds:F0} rows/s)"
        );
        output?.WriteLine($"Bulk: {bulkElapsed.TotalSeconds:F1} s ({scanned / bulkElapsed.TotalSeconds:F0} rows/s)");
        AssertEquivalent(
            await SnapshotAsync(perRow, false, ct),
            await SnapshotAsync(bulk, false, ct),
            ["outcomes", "runs", .. BusinessSections],
            "benchmark"
        );
    }

    private sealed class Clone(string connectionString, DataMigrationOptions options)
    {
        public string ConnectionString => connectionString;
        public DataMigrationOptions Options => options;

        /// <summary>Highest AuditRecords id after clone setup (MyTeam); later audits belong to Training runs.</summary>
        public long AuditFloor { get; set; }

        // Production contexts stamp CreatedById/UpdatedById through AuditableEntityInterceptor (background
        // saves resolve to the System user); include it so the Bulk stamping path is compared too.
        public UnifiedDbContext Db() =>
            new(
                new DbContextOptionsBuilder<UnifiedDbContext>()
                    .UseNpgsql(connectionString)
                    .AddInterceptors(new AuditableEntityInterceptor(new SystemActor()))
                    .Options
            );
    }

    private static UnifiedDbContext Db(string connectionString) =>
        new(new DbContextOptionsBuilder<UnifiedDbContext>().UseNpgsql(connectionString).Options);

    private static string U(int n) => $"00000000-0000-0000-0000-0000000001{n:00}";

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
            );
            await command.ExecuteNonQueryAsync(ct);
            databases.Add(name);
        }
        return new NpgsqlConnectionStringBuilder(admin.ConnectionString)
        {
            Database = name,
            Pooling = false,
        }.ConnectionString;
    }

    private DataMigrationOptions NewOptions(MigrationPersistenceMode mode, int chunk)
    {
        var options = new DataMigrationOptions();
        options.Sources.SS.Enabled = true;
        options.Sources.SS.ConnectionString = sourceConnection;
        options.Sources.SS.CutoverDate = new DateOnly(2026, 12, 1);
        foreach (var job in DataMigrationOperations.Jobs)
        {
            options.Sources.SS.Jobs.For(job).Enabled = true;
            options.Sources.SS.Jobs.For(job).AllowMigrationApply = true;
        }
        options.Sources.SS.Jobs.Training.PersistenceMode = mode;
        options.Sources.SS.Jobs.Training.BulkChunkSize = chunk;
        return options;
    }

    /// <summary>Clones the migrated baseline, runs MyTeam through the real path and removes one user crosswalk.</summary>
    private async Task<Clone> CloneAsync(
        string name,
        MigrationPersistenceMode mode,
        CancellationToken ct,
        int chunk = 4
    )
    {
        var connection = await CreateDatabaseAsync($"migration_it_bulk_{name}_{suffix}", baseDatabase, ct);
        var clone = new Clone(
            new NpgsqlConnectionStringBuilder(connection) { Pooling = true }.ConnectionString,
            NewOptions(mode, chunk)
        );
        var myTeam = await RunAsync(clone, "MyTeam", ct);
        await AssertRunAsync(clone, myTeam, "Completed", ct);
        // Missing user parent: the source user exists but its MyTeam crosswalk does not.
        await ExecuteAsync(
            clone.ConnectionString,
            $"""
            DELETE FROM "DataMigrationMutations" WHERE "DataMigrationRecordId" IN
                (SELECT "Id" FROM "DataMigrationRecords" WHERE "EntityType"='User' AND "LegacyId"='{U(UnlinkedUser)}');
            DELETE FROM "DataMigrationRecords" WHERE "EntityType"='User' AND "LegacyId"='{U(UnlinkedUser)}';
            """,
            ct
        );
        clone.AuditFloor = await CountAsync(clone, "SELECT COALESCE(MAX(\"Id\"),0) FROM \"AuditRecords\"", ct);
        return clone;
    }

    private SsMigrationExecutor Executor(
        UnifiedDbContext context,
        DataMigrationOptions options,
        SsTrainingBulkWriter? bulk = null
    ) =>
        new(
            context,
            configuration,
            Options.Create(options),
            flags,
            new DataMigrationControlService(context),
            new SsSnapshotReader(Options.Create(options)),
            new SsMyTeamWriter(context, new SsLeaveWriter(context)),
            new SsTrainingWriter(context),
            new SsSchedulingWriter(context),
            rollbackJournal: new SsRollbackJournal(context, configuration),
            trainingBulk: bulk ?? new SsTrainingBulkWriter(context, MsOptions.Create(AuditOptions))
        );

    private DataMigrationOperations Operations(UnifiedDbContext context, DataMigrationOptions options)
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(UnifiedClaimTypes.UserId, actor.ToString())], "test")
        );
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        return new(
            context,
            Options.Create(options),
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
        string job,
        CancellationToken ct,
        bool dryRun = false,
        Func<int, CancellationToken, Task>? hook = null,
        bool expectFailure = false
    )
    {
        await using var context = clone.Db();
        var accepted = await Operations(context, clone.Options).TriggerAsync("SS", job, dryRun, actor, ct);
        await using var worker = clone.Db();
        var writer = new SsTrainingBulkWriter(worker, MsOptions.Create(AuditOptions)) { BeforeChunkCommitAsync = hook };
        var execute = () => Executor(worker, clone.Options, writer).ExecuteAcquiredAsync(accepted.RunId, ct);
        if (expectFailure)
            await Assert.ThrowsAsync<InvalidOperationException>(execute);
        else
            await execute();
        return accepted.RunId;
    }

    /// <summary>Training runs finish Failed when the fixture's expected rejections exist; no technical failure.</summary>
    private static async Task AssertCleanAsync(Clone clone, Guid runId, CancellationToken ct)
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
        Assert.Equal(0, run.FailedCount);
        Assert.Equal(run.RejectedCount > 0 ? "Failed" : "Completed", run.Status);
    }

    private async Task AssertRunAuditAsync(Clone clone, Guid runId, long auditFloor, CancellationToken ct)
    {
        await using var db = clone.Db();
        var run = await db.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == runId, ct);
        var audits = await db.AuditRecords.AsNoTracking().Where(x => x.Id > auditFloor).ToListAsync(ct);
        // No business entity or bookkeeping audit may leak from either executor path.
        Assert.Equal(2, audits.Count);
        var trigger = Assert.Single(audits, x => x.EntityType == "DataMigrationOperation");
        var summary = Assert.Single(audits, x => x.EntityType == "DataMigrationRunSummary");
        Assert.Equal(actor, trigger.ActorUserId);
        Assert.Equal(trigger.ActorUserId, summary.ActorUserId);
        Assert.Equal(runId.ToString("D"), summary.EntityPK);
        Assert.Equal("DataMigrationRuns", summary.TableName);
        Assert.Equal("Added", summary.Action);
        Assert.Equal(
            await db.DataMigrationRuns.CountAsync(ct),
            await db.AuditRecords.CountAsync(x => x.EntityType == "DataMigrationRunSummary", ct)
        );

        using var document = JsonDocument.Parse(summary.NewValues!);
        var values = document.RootElement;
        Assert.Equal(runId, values.GetProperty("RunId").GetGuid());
        Assert.Equal(run.Source, values.GetProperty("Source").GetString());
        Assert.Equal(run.Job, values.GetProperty("Job").GetString());
        Assert.Equal(
            clone.Options.Sources.SS.Jobs.Training.PersistenceMode.ToString(),
            values.GetProperty("PersistenceMode").GetString()
        );
        Assert.Equal(run.Status, values.GetProperty("Status").GetString());
        Assert.Equal(run.DryRun, values.GetProperty("DryRun").GetBoolean());
        Assert.Equal(actor, values.GetProperty("Initiator").GetProperty("UserId").GetGuid());
        Assert.Equal("Operator", values.GetProperty("Initiator").GetProperty("Kind").GetString());
        var counts = values.GetProperty("Counts");
        foreach (
            var (name, expected) in new[]
            {
                (nameof(run.ScannedCount), run.ScannedCount),
                (nameof(run.InsertedCount), run.InsertedCount),
                (nameof(run.UpdatedCount), run.UpdatedCount),
                (nameof(run.UnchangedCount), run.UnchangedCount),
                (nameof(run.SkippedCount), run.SkippedCount),
                (nameof(run.RejectedCount), run.RejectedCount),
                (nameof(run.FailedCount), run.FailedCount),
                (nameof(run.CommittedBatchCount), run.CommittedBatchCount),
            }
        )
            Assert.Equal(expected, counts.GetProperty(name).GetInt32());

        var fingerprints = values.GetProperty("Fingerprints");
        var source = fingerprints.GetProperty("SourceProjection");
        Assert.Equal("Available", source.GetProperty("Status").GetString());
        var snapshot = await new SsSnapshotReader(Options.Create(clone.Options)).ReadAsync(
            SsTrainingWriter.Tables,
            clone.Options.Sources.SS.Jobs.Training.BatchSize,
            ct
        );
        var sourceDigest = SsMigrationFingerprints.Source(snapshot, ct);
        Assert.Equal(snapshot.Values.Sum(x => (long)x.Count), source.GetProperty("Rows").GetInt64());
        Assert.Equal(run.AuditSourceRowCount, source.GetProperty("Rows").GetInt64());
        Assert.Equal(
            run.AuditSourceFingerprint,
            "ss-source-projection-v1:sha256:" + source.GetProperty("Value").GetString()
        );
        AssertFingerprint(source, sourceDigest, "ss-source-projection-v1");
        var outcomes = fingerprints.GetProperty("OutcomeEvidence");
        Assert.Equal(
            await db.DataMigrationOutcomes.LongCountAsync(x => x.RunId == runId, ct),
            outcomes.GetProperty("Rows").GetInt64()
        );
        AssertFingerprint(
            outcomes,
            await SsMigrationFingerprints.OutcomesAsync(db, runId, ct),
            "ss-outcome-evidence-v1"
        );
        var crosswalks = fingerprints.GetProperty("AppliedCrosswalkEvidence");
        Assert.Equal("Available", fingerprints.GetProperty("AppliedCrosswalkStatus").GetString());
        Assert.Equal(
            await db.DataMigrationRecords.LongCountAsync(x => x.Source == "SS" && x.LastAppliedRunId == runId, ct),
            crosswalks.GetProperty("Rows").GetInt64()
        );
        AssertFingerprint(
            crosswalks,
            await SsMigrationFingerprints.CrosswalksAsync(db, runId, ct),
            "ss-applied-crosswalk-evidence-v1"
        );
        Assert.Equal("UnavailableNotCaptured", fingerprints.GetProperty("FullSource").GetString());
        Assert.Equal("UnavailableNotCaptured", fingerprints.GetProperty("FullTarget").GetString());
    }

    private static void AssertFingerprint(JsonElement value, SsMigrationFingerprints.Digest expected, string format)
    {
        Assert.Equal(format, value.GetProperty("Format").GetString());
        Assert.Equal("SHA-256", value.GetProperty("Algorithm").GetString());
        Assert.Matches("^[A-F0-9]{64}$", value.GetProperty("Value").GetString()!);
        Assert.Equal(expected.Value, value.GetProperty("Value").GetString());
        Assert.Equal(expected.Rows, value.GetProperty("Rows").GetInt64());
    }

    private static async Task AssertRunAsync(Clone clone, Guid runId, string status, CancellationToken ct)
    {
        await using var context = clone.Db();
        var run = await context.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == runId, ct);
        if (run.Status != status)
        {
            var details = await context
                .DataMigrationOutcomes.AsNoTracking()
                .Where(x => x.RunId == runId && (x.Outcome == "Rejected" || x.Outcome == "Failed"))
                .Select(x => x.EntityType + ":" + x.LegacyId + ":" + x.Outcome + ":" + x.Reason)
                .ToListAsync(ct);
            var failures = await context
                .DataMigrationFailures.AsNoTracking()
                .Where(x => x.RunId == runId)
                .Select(x => x.ErrorType + ":" + x.ErrorMessage)
                .ToListAsync(ct);
            Assert.Fail($"{run.Job} run {run.Status}: {string.Join("; ", details.Concat(failures))}");
        }
    }

    /// <summary>"Outcome:Reason" of one source row in a run, or "" when the run has no outcome for it.</summary>
    private static async Task<string> OutcomeAsync(
        Clone clone,
        Guid runId,
        string type,
        string legacyId,
        CancellationToken ct
    )
    {
        await using var context = clone.Db();
        return await context
                .DataMigrationOutcomes.AsNoTracking()
                .Where(x => x.RunId == runId && x.EntityType == type && x.LegacyId == legacyId)
                .Select(x => x.Outcome + ":" + x.Reason)
                .SingleOrDefaultAsync(ct)
            ?? "";
    }

    /// <summary>Mapped target values of the UserTraining owned by a SheriffTraining crosswalk.</summary>
    private static async Task<string> TargetHistoryAsync(Clone clone, string legacyId, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(clone.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            $"""
            SELECT u."AwardedOn"::text || '|' || u."EndingOn"::text || '|' || COALESCE(u."Notes",'') || '|'
                || u."Version"::text || '|' || COALESCE(u."UpdatedOn"::text,'')
            FROM "UserTrainings" u JOIN "DataMigrationRecords" r
              ON r."TargetType"='UserTraining' AND r."TargetKey"=u."Id"::text
            WHERE r."Source"='SS' AND r."EntityType"='SheriffTraining' AND r."LegacyId"='{legacyId}'
            """,
            connection
        );
        return (string?)await command.ExecuteScalarAsync(ct) ?? throw new InvalidOperationException(legacyId);
    }

    private static async Task<HashSet<string?>> ReasonsAsync(Clone clone, Guid runId, CancellationToken ct)
    {
        await using var context = clone.Db();
        return (
            await context
                .DataMigrationOutcomes.AsNoTracking()
                .Where(x => x.RunId == runId)
                .Select(x => x.Reason)
                .ToListAsync(ct)
        ).ToHashSet();
    }

    private static async Task AssertNoDuplicatesOrOrphansAsync(Clone clone, CancellationToken ct)
    {
        Assert.Equal(
            0,
            await CountAsync(
                clone,
                """
                SELECT COUNT(*) FROM (SELECT 1 FROM "DataMigrationRecords"
                GROUP BY "Source","EntityType","LegacyId" HAVING COUNT(*) > 1) d
                """,
                ct
            )
        );
        Assert.Equal(
            0,
            await CountAsync(
                clone,
                """
                SELECT COUNT(*) FROM (SELECT 1 FROM "UserTrainings"
                GROUP BY "UserId","TrainingId","Version" HAVING COUNT(*) > 1) d
                """,
                ct
            )
        );
        // Every target UserTraining is owned through exactly one SheriffTraining crosswalk and vice versa.
        Assert.Equal(
            0,
            await CountAsync(
                clone,
                """
                SELECT COUNT(*) FROM "UserTrainings" u WHERE NOT EXISTS (SELECT 1 FROM "DataMigrationRecords" r
                WHERE r."EntityType"='SheriffTraining' AND r."TargetType"='UserTraining' AND r."TargetKey"=u."Id"::text)
                """,
                ct
            )
        );
        Assert.Equal(
            0,
            await CountAsync(
                clone,
                """
                SELECT COUNT(*) FROM "DataMigrationRecords" r WHERE r."EntityType"='SheriffTraining'
                AND NOT EXISTS (SELECT 1 FROM "UserTrainings" u WHERE u."Id"::text=r."TargetKey")
                """,
                ct
            )
        );
        Assert.Equal(
            0,
            await CountAsync(
                clone,
                """
                SELECT COUNT(*) FROM "DataMigrationRecords" r WHERE r."EntityType"='Training'
                AND NOT EXISTS (SELECT 1 FROM "Trainings" t WHERE t."Id"::text=r."TargetKey")
                """,
                ct
            )
        );
    }

    private static async Task<long> CountAsync(Clone clone, string sql, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(clone.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteAsync(string connectionString, string sql, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 300 };
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task EditTargetsAsync(Clone clone, CancellationToken ct) =>
        // Target-side edits: one changes in the source too (drift), one does not (unchanged source).
        await ExecuteAsync(
            clone.ConnectionString,
            """
            UPDATE "UserTrainings" SET "Notes"='edited in Unified'
            WHERE "Id"::text IN (SELECT "TargetKey" FROM "DataMigrationRecords"
                WHERE "EntityType"='SheriffTraining' AND "LegacyId" IN ('1072','1093'));
            """,
            ct
        );

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
            problems.Add($"[{section}] only PerRow/expected:");
            problems.AddRange(missing.Take(25).Select(x => "  - " + x));
            problems.Add($"[{section}] only Bulk/actual:");
            problems.AddRange(extra.Take(25).Select(x => "  + " + x));
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
    /// Normalized state: generated IDs are replaced by source keys (crosswalk LegacyId, course code,
    /// user legacy ID), run IDs and run-derived timestamps by job ordinals. Batch numbers/sequences are excluded.
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
            await using var command = new NpgsqlCommand(sql, connection);
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
        var startLabels = new Dictionary<DateTime, string>();
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
                    startLabels.TryAdd(started, label);
            }
        }
        var userLegacy = (
            await Query("""SELECT "TargetKey","LegacyId" FROM "DataMigrationRecords" WHERE "EntityType"='User' """)
        ).ToDictionary(x => (string)x[0]!, x => "U:" + (string)x[1]!);
        // Users without a crosswalk (deliberately unlinked fixture) normalize by their unique IDIR name.
        foreach (var user in await Query("""SELECT "Id"::text,"IdirName" FROM "Users" """))
            userLegacy.TryAdd((string)user[0]!, "idir:" + user[1]);
        var courseCodes = (await Query("""SELECT "Id"::text,"Code" FROM "Trainings" """)).ToDictionary(
            x => (string)x[0]!,
            x => "C:" + (string)x[1]!
        );
        var historyLegacy = (
            await Query(
                """SELECT "TargetKey","LegacyId" FROM "DataMigrationRecords" WHERE "TargetType"='UserTraining' """
            )
        ).ToDictionary(x => (string)x[0]!, x => "H:" + (string)x[1]!);

        string Format(object? value) =>
            value switch
            {
                null => "null",
                DateTime time when startLabels.TryGetValue(time, out var label) => "@" + label,
                DateTime time => time.ToString("O", CultureInfo.InvariantCulture),
                Guid id when runLabels.TryGetValue(id, out var label) => label,
                Guid id when userLegacy.TryGetValue(id.ToString(), out var user) => user,
                Guid id => id.ToString(),
                string[] items => "[" + string.Join(",", items.Order(StringComparer.Ordinal)) + "]",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()!,
            };
        async Task<List<string>> Lines(string sql, Func<object?[], object?[]>? map = null) =>
            (await Query(sql))
                .Select(x => string.Join(" | ", (map?.Invoke(x) ?? x).Select(Format)))
                .Order(StringComparer.Ordinal)
                .ToList();
        string Target(string type, string key) =>
            type switch
            {
                "Training" => courseCodes.GetValueOrDefault(key, "missing:" + key),
                "UserTraining" => historyLegacy.GetValueOrDefault(key, "missing:" + key),
                "User" => userLegacy.GetValueOrDefault(key, key),
                _ => type + ":" + key,
            };

        // Audit primary keys embed target GUIDs; normalize them through the same crosswalk/code maps.
        string AuditKey(string table, string pk) =>
            table switch
            {
                "Trainings" => courseCodes.GetValueOrDefault(pk, "missing:" + pk),
                "UserTrainings" => historyLegacy.GetValueOrDefault(pk, "missing:" + pk),
                _ => Regex.Replace(
                    pk,
                    "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
                    m =>
                        historyLegacy.GetValueOrDefault(m.Value)
                        ?? courseCodes.GetValueOrDefault(m.Value)
                        ?? userLegacy.GetValueOrDefault(m.Value)
                        ?? "guid"
                ),
            };

        var trainingRuns = """(SELECT "Id" FROM "DataMigrationRuns" WHERE "Job"='Training' AND NOT "DryRun")""";
        return new()
        {
            ["runs"] = await Lines(
                """
                SELECT "Id","Status","ScannedCount","InsertedCount","UpdatedCount","UnchangedCount","SkippedCount",
                       "RejectedCount","FailedCount","HeartbeatOn" IS NOT NULL,"CompletedOn" IS NOT NULL,"MyTeamRunId"
                FROM "DataMigrationRuns" WHERE "Job"='Training' AND NOT "DryRun"
                """
            ),
            ["outcomes"] = await Lines(
                $"""
                SELECT "RunId","EntityType","LegacyId","Outcome","Reason" FROM "DataMigrationOutcomes"
                WHERE "RunId" IN {trainingRuns}
                """
            ),
            ["failures"] = await Lines(
                $"""
                SELECT "RunId","EntityType","LegacyId","ErrorType","Watermark" FROM "DataMigrationFailures"
                WHERE "RunId" IN {trainingRuns}
                """
            ),
            ["workitems"] = await Lines(
                """
                SELECT "Stream","EntityType","LegacyId","Outcome","Reason","PendingRetry","Attempts","LastRunId",
                       "SourceChangedAtUtc"
                FROM "DataMigrationWorkItems" WHERE "Job"='Training'
                """
            ),
            ["checkpoints"] = await Lines(
                """
                SELECT "Stream","EntityType","LegacyId","IdKind","SourceChangedAtUtc","UpperLegacyId",
                       "UpperChangedAtUtc","LastRunId","HistoryStartDate"
                FROM "DataMigrationCheckpoints" WHERE "Job"='Training'
                """
            ),
            ["trainings"] = await Lines(
                """
                SELECT t."Code",t."Description",t."EffectiveDate",t."ExpiryDate",t."Mandatory",t."ValidityDays",
                       t."AdvanceNoticeDays",t."Rotating",c."Name",t."Order",t."CreatedOn",t."UpdatedOn",
                       t."CreatedById",t."UpdatedById"
                FROM "Trainings" t LEFT JOIN "TrainingCategories" c ON c."Id"=t."TrainingCategoryId"
                """
            ),
            ["categories"] = await Lines(
                """SELECT "Name","Description","CreatedOn","CreatedById","UpdatedOn" FROM "TrainingCategories" """
            ),
            ["requirements"] = await Lines(
                """
                SELECT t."Code",p."Code",r."CreatedOn",r."CreatedById"
                FROM "TrainingMandatoryTrainingProfiles" r
                JOIN "Trainings" t ON t."Id"=r."TrainingId"
                JOIN "TrainingProfileTypes" p ON p."Id"=r."TrainingProfileTypeId"
                """
            ),
            ["usertrainings"] = await Lines(
                """
                SELECT u."Id"::text,u."UserId",t."Code",u."Version",u."AwardedOn",u."EndingOn",u."AllDay",
                       u."ExpiryDate",u."NoticeState",u."Notes",u."CreatedOn",u."UpdatedOn",u."CreatedById",
                       u."UpdatedById"
                FROM "UserTrainings" u JOIN "Trainings" t ON t."Id"=u."TrainingId"
                """,
                x => [historyLegacy.GetValueOrDefault((string)x[0]!, "unlinked"), .. x[1..]]
            ),
            ["users"] = await Lines(
                """
                SELECT u."Id",p."Code" FROM "Users" u
                LEFT JOIN "TrainingProfileTypes" p ON p."Id"=u."TrainingProfileId"
                """
            ),
            ["userstamps"] = await Lines("""SELECT "Id","UpdatedOn","UpdatedById" FROM "Users" """),
            ["records"] = await Lines(
                """
                SELECT "EntityType","LegacyId","TargetType","TargetKey","PayloadHash","SourceUpdatedAtUtc",
                       "CreatedByRunId","LastAppliedRunId","IsDeleted"
                FROM "DataMigrationRecords" WHERE "EntityType" IN ('Training','SheriffTraining','SheriffStandardTraining')
                """,
                x => [x[0], x[1], x[2], Target((string)x[2]!, (string)x[3]!), .. x[4..]]
            ),
            ["mutations"] = await Lines(
                $"""
                SELECT r."EntityType",r."LegacyId",m."Operation",m."RunId",m."BeforeImage" IS NULL,m."Version" IS NULL
                FROM "DataMigrationMutations" m JOIN "DataMigrationRecords" r ON r."Id"=m."DataMigrationRecordId"
                WHERE m."RunId" IN {trainingRuns}
                """
            ),
            ["audit"] = await Lines(
                $"""
                SELECT "TableName","EntityType","Action","ChangedColumns","ActorUserId","ActorName",
                    (SELECT string_agg(k, ',' ORDER BY k) FROM jsonb_object_keys("NewValues") k),
                    (SELECT string_agg(k, ',' ORDER BY k) FROM jsonb_object_keys("OldValues") k)
                    ,"EntityPK",
                    "NewValues"->>'CreatedById' IS NULL,"NewValues"->>'UpdatedById' IS NULL,
                    "OldValues"->>'UpdatedById' IS NULL
                FROM "AuditRecords"
                WHERE "TableName" IN ('Trainings','TrainingCategories','TrainingMandatoryTrainingProfiles',
                    'UserTrainings','Users')
                AND "Id" > {clone.AuditFloor}
                """,
                x => [.. x[..8], AuditKey((string)x[0]!, (string)x[8]!), .. x[9..]]
            ),
        };
    }

    private static string FindSourceSchema()
    {
        for (
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            var path = Path.Combine(
                directory.FullName,
                "docs",
                "data-migration",
                "ss-implementation-baseline",
                "evidence",
                "ss-schema-noseed.sql"
            );
            if (File.Exists(path))
                return path;
        }
        throw new FileNotFoundException("Repository SS synthetic schema not found.");
    }

    private static string SourceFixture()
    {
        var unknown = "ffffffff-0000-0000-0000-000000000001";
        return $"""
            TRUNCATE "Region","Location","LookupCode","LookupSortOrder","Role","User","UserRole",
                "SheriffActingRank","SheriffAwayLocation","SheriffLeave","SheriffTraining","SheriffStandardTraining" CASCADE;
            -- Allow orphan source references (unknown user / unknown course) as adverse fixtures.
            ALTER TABLE "SheriffTraining" DROP CONSTRAINT "FK_SheriffTraining_User_SheriffId";
            ALTER TABLE "SheriffTraining" DROP CONSTRAINT "FK_SheriffTraining_LookupCode_TrainingTypeId";
            ALTER TABLE "SheriffTraining" ALTER COLUMN "TrainingTypeId" DROP NOT NULL;
            INSERT INTO "User" ("Id","CreatedOn","IdirId","Discriminator","IdirName","IsEnabled","FirstName",
                "LastName","Email","Excused")
            SELECT ('00000000-0000-0000-0000-0000000001' || lpad(n::text,2,'0'))::uuid, '2024-01-01Z',
                ('10000000-0000-0000-0000-0000000001' || lpad(n::text,2,'0'))::uuid, 'Sheriff', 'synthetic' || n,
                true, 'First' || n, 'Last' || n, 'user' || n || '@example.invalid', n = {ExcusedUser}
            FROM generate_series(1,{Users}) n;
            INSERT INTO "LookupCode" ("Id","Type","Code","SubCode","Description","EffectiveDate","ExpiryDate",
                "AdvanceNotice","Category","Mandatory","ValidityPeriod","Rotating","CreatedOn") VALUES
            (100,6,'First Aid',NULL,'Standard first aid','2019-01-01Z',NULL,30,'Provincial Standard',true,1095,false,'2024-01-01Z'),
            (101,6,'Other',NULL,'Other training',NULL,NULL,0,NULL,false,0,false,'2024-01-01Z'),
            (102,6,'Existing Course',NULL,'Links to pre-existing target',NULL,NULL,0,NULL,false,0,false,'2024-01-01Z'),
            (103,6,'   ',NULL,'Blank code',NULL,NULL,0,NULL,false,0,false,'2024-01-01Z'),
            (104,6,'Carbine Initial Course',NULL,'Carbine','2019-01-01Z',NULL,0,'Branch',true,0,false,'2024-01-01Z'),
            (105,6,'Use Of Force Recertification Program Module Number Alpha',NULL,'Long A',NULL,NULL,0,'Local',false,0,false,'2024-01-01Z'),
            (106,6,'Use Of Force Recertification Program Module Number Beta',NULL,'Long B',NULL,'2030-01-01Z',0,NULL,false,365,false,'2024-01-01Z'),
            (107,6,'Bad Category Course',NULL,'Bad category',NULL,NULL,0,'Weird',false,0,false,'2024-01-01Z'),
            (108,6,'Dup',NULL,'Dup one',NULL,NULL,0,NULL,false,0,false,'2024-01-01Z'),
            (109,6,'Dup',NULL,'Dup two',NULL,NULL,0,NULL,false,0,false,'2024-01-01Z'),
            (110,6,'Scoped','X','Scoped',NULL,NULL,0,NULL,false,0,false,'2024-01-01Z'),
            (111,6,'Carbine Practice 1',NULL,'Not mandatory',NULL,NULL,0,NULL,false,0,false,'2024-01-01Z'),
            (112,6,'Carbine Annual Recertification Extended Long Name Over Fifty Chars',NULL,'Shortened carbine',NULL,NULL,0,NULL,true,0,false,'2024-01-01Z'),
            (113,6,'Rotating Course',NULL,'Repeated histories',NULL,NULL,0,NULL,false,0,false,'2024-01-01Z'),
            (114,6,'Bad Notice',NULL,'Notice beyond validity',NULL,NULL,20,NULL,false,10,false,'2024-01-01Z'),
            (115,6,'Four Year',NULL,'1461 days maps to 1460',NULL,NULL,0,NULL,false,1461,false,'2024-01-01Z'),
            (116,6,'Negative',NULL,'Negative validity',NULL,NULL,0,NULL,false,-1,false,'2024-01-01Z'),
            (117,6,'Long Description',NULL,'{new string('d', 201)}',NULL,NULL,0,NULL,false,0,false,'2024-01-01Z'),
            (118,6,'Very Long Validity',NULL,'Capped at 1825',NULL,NULL,0,NULL,false,4000,true,'2024-01-01Z');
            INSERT INTO "SheriffTraining" ("Id","SheriffId","TrainingTypeId","StartDate","EndDate","ExpiryDate",
                "TrainingCertificationExpiry","FirstNotice","Comment","Note","Timezone","CreatedOn")
            SELECT 1000 + u*10 + y, ('00000000-0000-0000-0000-0000000001' || lpad(u::text,2,'0'))::uuid, 100,
                make_timestamptz(2020+y,6,15,16,0,0,'UTC'), make_timestamptz(2020+y,6,16,0,0,0,'UTC'), NULL,
                make_timestamptz(2023+y,6,15,16,0,0,'UTC'), y % 2 = 0, NULL, 'note ' || u || '-' || y, NULL,
                '2024-01-01Z'
            FROM generate_series(1,10) u, generate_series(0,4) y;
            INSERT INTO "SheriffTraining" ("Id","SheriffId","TrainingTypeId","StartDate","EndDate","ExpiryDate",
                "TrainingCertificationExpiry","FirstNotice","Comment","Note","Timezone","CreatedOn") VALUES
            (2001,'{U(
                1
            )}',101,'2023-01-10T17:00Z','2023-01-11T01:00Z',NULL,NULL,false,'CPR refresher','kept note',NULL,'2024-01-01Z'),
            (2002,'{U(
                2
            )}',100,'2023-02-10T17:00Z','2023-02-11T01:00Z',NULL,NULL,false,'unexpected',NULL,NULL,'2024-01-01Z'),
            (2003,'{U(3)}',103,'2023-03-10T17:00Z','2023-03-11T01:00Z',NULL,NULL,false,NULL,NULL,NULL,'2024-01-01Z'),
            (2004,'{U(3)}',111,'2023-03-12T17:00Z','2023-03-13T01:00Z',NULL,NULL,false,NULL,NULL,NULL,'2024-01-01Z'),
            (2005,'{U(
                4
            )}',104,'2023-04-10T16:00Z','2023-04-11T00:00Z',NULL,'2024-04-10T16:00Z',true,NULL,NULL,NULL,'2024-01-01Z'),
            (2006,'{U(5)}',112,'2023-05-10T16:00Z','2023-05-11T00:00Z',NULL,NULL,false,NULL,NULL,NULL,'2024-01-01Z'),
            (2007,'{U(
                ExcusedUser
            )}',100,'2023-06-10T16:00Z','2023-06-11T00:00Z',NULL,NULL,false,NULL,NULL,NULL,'2024-01-01Z'),
            (2008,'{U(
                6
            )}',100,'2023-07-10T16:00Z','2023-07-11T00:00Z','2023-08-01Z',NULL,false,NULL,NULL,NULL,'2024-01-01Z'),
            (2009,'{U(6)}',100,'2019-12-30T16:00Z','2020-01-01T07:59:59Z',NULL,NULL,false,NULL,NULL,NULL,'2024-01-01Z'),
            (2010,'{U(
                6
            )}',113,'2020-01-01T08:00:00Z','2020-01-01T08:00:00Z',NULL,NULL,false,NULL,NULL,NULL,'2024-01-01Z'),
            (2011,'{unknown}',100,'2023-08-10T16:00Z','2023-08-11T00:00Z',NULL,NULL,false,NULL,NULL,NULL,'2024-01-01Z'),
            (2012,'{U(
                UnlinkedUser
            )}',100,'2023-09-10T16:00Z','2023-09-11T00:00Z',NULL,NULL,false,NULL,NULL,NULL,'2024-01-01Z'),
            (2013,'{U(7)}',100,'2023-10-11T16:00Z','2023-10-10T16:00Z',NULL,NULL,false,NULL,NULL,NULL,'2024-01-01Z'),
            (2014,'{U(
                7
            )}',113,'2024-03-10T09:30Z','2024-03-10T10:30Z',NULL,NULL,false,NULL,'spring forward','America/Vancouver','2024-01-01Z'),
            (2015,'{U(
                7
            )}',113,'2024-11-03T08:30Z','2024-11-03T09:30Z',NULL,NULL,false,NULL,'fall back','America/Vancouver','2024-01-01Z'),
            (2016,'{U(
                8
            )}',100,'2023-11-10T16:00Z','2023-11-11T00:00Z',NULL,NULL,false,NULL,NULL,'Not/AZone','2024-01-01Z'),
            (2017,'{U(8)}',113,'2023-01-10T17:00Z','2023-01-11T01:00Z',NULL,NULL,false,NULL,NULL,NULL,'2024-01-01Z'),
            (2018,'{U(8)}',113,'2024-01-10T17:00Z','2024-01-11T01:00Z',NULL,NULL,false,NULL,NULL,NULL,'2024-01-01Z'),
            (2019,'{U(9)}',105,'2023-02-10T17:00Z','2023-02-11T01:00Z',NULL,NULL,false,NULL,NULL,NULL,'2024-01-01Z'),
            (2020,'{U(
                9
            )}',106,'2023-02-12T17:00Z','2023-02-13T01:00Z',NULL,'2026-02-12T17:00Z',false,NULL,NULL,NULL,'2024-01-01Z'),
            (2021,'{U(10)}',102,'2023-03-10T17:00Z','2023-03-11T01:00Z',NULL,NULL,true,NULL,NULL,NULL,'2024-01-01Z'),
            (2022,'{U(1)}',107,'2023-04-10T16:00Z','2023-04-11T00:00Z',NULL,NULL,false,NULL,NULL,NULL,'2024-01-01Z'),
            (2023,'{U(2)}',108,'2023-05-10T16:00Z','2023-05-11T00:00Z',NULL,NULL,false,NULL,NULL,NULL,'2024-01-01Z'),
            (2024,'{U(
                4
            )}',104,'2024-04-10T16:00Z','2024-04-11T00:00Z',NULL,'2025-04-10T16:00Z',false,NULL,NULL,NULL,'2024-01-01Z'),
            (2025,'{U(
                10
            )}',115,'2023-06-10T16:00Z','2023-06-11T00:00Z',NULL,'2027-06-10T16:00Z',true,NULL,NULL,NULL,'2024-01-01Z'),
            (2026,'{U(9)}',101,'2023-07-10T16:00Z','2023-07-11T00:00Z',NULL,NULL,false,NULL,'{new string(
                'n',
                200
            )}',NULL,'2024-01-01Z'),
            (2027,'{U(5)}',NULL,'2023-08-10T16:00Z','2023-08-11T00:00Z',NULL,NULL,false,NULL,NULL,NULL,'2024-01-01Z'),
            (2028,'{U(5)}',999,'2023-08-12T16:00Z','2023-08-13T00:00Z',NULL,NULL,false,NULL,NULL,NULL,'2024-01-01Z'),
            (2029,'{U(
                1
            )}',101,'2023-09-10T16:00Z','2023-09-11T00:00Z',NULL,NULL,false,'same text','same text',NULL,'2024-01-01Z'),
            (2030,'{U(2)}',118,'2023-09-12T16:00Z','2023-09-13T00:00Z',NULL,NULL,false,NULL,NULL,NULL,'2024-01-01Z'),
            (2031,'{U(
                3
            )}',100,'2020-06-15T16:00Z','2020-06-16T00:00Z',NULL,NULL,false,NULL,'same end as v1',NULL,'2024-01-01Z');
            INSERT INTO "SheriffStandardTraining" ("Id","TrainingTypeId","CreatedOn") VALUES (1,100,'2024-01-01Z');
            """;
    }

    private static string RoundFourSource() =>
        $"""
            ALTER TABLE "SheriffTraining" ALTER COLUMN "Note" TYPE text;
            UPDATE "SheriffTraining" SET "Note"=repeat('x', 2001), "UpdatedOn"='2025-07-01Z' WHERE "Id"=1073;
            UPDATE "SheriffTraining" SET "Note"='note-only change', "UpdatedOn"='2025-07-01Z' WHERE "Id"=1074;
            INSERT INTO "SheriffTraining" ("Id","SheriffId","TrainingTypeId","StartDate","EndDate","ExpiryDate",
                "TrainingCertificationExpiry","FirstNotice","Comment","Note","Timezone","CreatedOn") VALUES
            (4001,'{U(
                6
            )}',100,'2024-07-01T16:00Z','2024-07-02T00:00Z',NULL,NULL,false,NULL,repeat('y', 2001),NULL,'2025-07-01Z'),
            (4002,'{U(
                6
            )}',113,'2024-07-03T16:00Z','2024-07-04T00:00Z',NULL,NULL,false,NULL,repeat(chr(128512), 1001),NULL,'2025-07-01Z'),
            (4003,'{U(
                6
            )}',101,'2024-07-05T16:00Z','2024-07-06T00:00Z',NULL,NULL,false,NULL,repeat('z', 1995) || repeat(' ', 10),NULL,'2025-07-01Z'),
            (4004,'{U(
                6
            )}',102,'2024-07-07T16:00Z','2024-07-08T00:00Z',NULL,NULL,false,NULL,repeat(chr(128512), 1999) || '   ',NULL,'2025-07-01Z'),
            (4005,'{U(
                6
            )}',103,'2024-07-09T16:00Z','2024-07-10T00:00Z',NULL,NULL,false,NULL,repeat('q', 2000) || ' x',NULL,'2025-07-01Z');
            """;

    private static string RoundTwoSource() =>
        $"""
            UPDATE "SheriffTraining" SET "EndDate"="EndDate" + interval '1 day', "UpdatedOn"='2025-06-01Z' WHERE "Id"=1011;
            UPDATE "SheriffTraining" SET "TrainingTypeId"=102, "UpdatedOn"='2025-06-01Z' WHERE "Id"=1022;
            UPDATE "SheriffTraining" SET "SheriffId"='{U(4)}', "UpdatedOn"='2025-06-01Z' WHERE "Id"=1033;
            UPDATE "SheriffTraining" SET "ExpiryDate"='2025-05-01Z', "UpdatedOn"='2025-06-01Z' WHERE "Id"=1050;
            UPDATE "SheriffTraining" SET "UpdatedOn"='2025-06-01Z' WHERE "Id"=1061;
            UPDATE "SheriffTraining" SET "Note"='changed in SS', "UpdatedOn"='2025-06-01Z' WHERE "Id"=1072;
            UPDATE "SheriffTraining" SET "Comment"=NULL, "UpdatedOn"='2025-06-01Z' WHERE "Id"=2002;
            UPDATE "SheriffTraining" SET "TrainingCertificationExpiry"='2028-01-01Z', "FirstNotice"=true,
                "UpdatedOn"='2025-06-01Z' WHERE "Id"=1084;
            UPDATE "SheriffTraining" SET "TrainingTypeId"=104, "UpdatedOn"='2025-06-01Z' WHERE "Id"=1094;
            INSERT INTO "SheriffTraining" ("Id","SheriffId","TrainingTypeId","StartDate","EndDate","ExpiryDate",
                "TrainingCertificationExpiry","FirstNotice","Comment","Note","Timezone","CreatedOn") VALUES
            (3001,'{U(
                1
            )}',100,'2020-02-01T16:00Z','2020-02-02T00:00Z',NULL,NULL,false,NULL,'late older',NULL,'2025-06-01Z'),
            (3002,'{U(2)}',120,'2024-02-01T16:00Z','2024-02-02T00:00Z',NULL,NULL,false,NULL,NULL,NULL,'2025-06-01Z'),
            (3003,'{U(3)}',103,'2024-02-03T16:00Z','2024-02-04T00:00Z',NULL,NULL,false,NULL,NULL,NULL,'2025-06-01Z');
            INSERT INTO "LookupCode" ("Id","Type","Code","SubCode","Description","EffectiveDate","ExpiryDate",
                "AdvanceNotice","Category","Mandatory","ValidityPeriod","Rotating","CreatedOn") VALUES
            (120,6,'Branch Course',NULL,'Added later',NULL,NULL,0,'Branch',false,0,false,'2025-06-01Z');
            UPDATE "LookupCode" SET "Code"='Fixed Code', "UpdatedOn"='2025-06-01Z' WHERE "Id"=103;
            UPDATE "LookupCode" SET "Mandatory"=true, "UpdatedOn"='2025-06-01Z' WHERE "Id"=113;
            """;

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
