using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using Unified.Authorization.Claims;
using Unified.DataMigration.FeatureFlags;
using Unified.DataMigration.Models;
using Unified.DataMigration.Options;
using Unified.DataMigration.Services;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.DataMigration;

namespace Unified.Tests.DataMigration;

// Only run against a newly created, isolated disposable PostgreSQL instance.
// No HTTP requests are made: the authenticated context is constructed in memory.
public sealed class MigrationApplyPostgresTests : IAsyncLifetime
{
    private const string Variable = "UNIFIED_MIGRATION_ISOLATED_TEST_CONNECTION_STRING";
    private const string SkipReason = "Set UNIFIED_MIGRATION_ISOLATED_TEST_CONNECTION_STRING for isolated PostgreSQL.";
    private static string? Admin => Environment.GetEnvironmentVariable(Variable);
    public static bool IsConfigured => !string.IsNullOrWhiteSpace(Admin);

    private readonly string database = $"migration_it_{Guid.NewGuid():N}";
    private readonly string sourceDatabase = $"migration_source_{Guid.NewGuid():N}";
    private readonly Guid actor = Guid.NewGuid();
    private UnifiedDbContext db = null!;
    private string connectionString = "";
    private bool created;
    private bool sourceCreated;
    private readonly DataMigrationOptions options = new();
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
        admin.Pooling = false;
        await using (var connection = new NpgsqlConnection(admin.ConnectionString))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            created = true;
        }
        connectionString = new NpgsqlConnectionStringBuilder(admin.ConnectionString)
        {
            Database = database,
        }.ConnectionString;
        db = NewDb();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        options.Sources.SS.Enabled = true;
        // Port 1 is closed in the test container's isolated network namespace.
        options.Sources.SS.ConnectionString =
            "Host=127.0.0.1;Port=1;Database=unreachable;Username=synthetic;Password=synthetic;Timeout=1";
        options.Sources.SS.CutoverDate = new DateOnly(2026, 12, 1);
        foreach (var job in DataMigrationOperations.Jobs)
        {
            options.Sources.SS.Jobs.For(job).Enabled = true;
            options.Sources.SS.Jobs.For(job).AllowMigrationApply = true;
        }
    }

    private UnifiedDbContext NewDb() =>
        new(new DbContextOptionsBuilder<UnifiedDbContext>().UseNpgsql(connectionString).Options);

    private SsMigrationExecutor Executor(UnifiedDbContext context) =>
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
            rollbackJournal: new SsRollbackJournal(context, configuration)
        );

    private (DataMigrationOperations Operations, Dispatcher Dispatcher) Operations(UnifiedDbContext context)
    {
        var dispatcher = new Dispatcher(Executor(context));
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(UnifiedClaimTypes.UserId, actor.ToString())], "test")
        );
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        return (
            new(
                context,
                Options.Create(options),
                flags,
                new DataMigrationControlService(context),
                dispatcher,
                null!,
                http,
                new Authorization()
            ),
            dispatcher
        );
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsConfigured))]
    public async Task DryRunsApplyAndRollbackGuard_UseRealSyntheticRegionAndNonAllowlistedTarget()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = new NpgsqlConnectionStringBuilder(Admin) { Pooling = false };
        await using (var connection = new NpgsqlConnection(admin.ConnectionString))
        {
            await connection.OpenAsync(ct);
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{sourceDatabase}\"", connection);
            await create.ExecuteNonQueryAsync(ct);
            sourceCreated = true;
        }
        var sourceConnection = new NpgsqlConnectionStringBuilder(admin.ConnectionString)
        {
            Database = sourceDatabase,
        }.ConnectionString;
        await using (var source = new NpgsqlConnection(sourceConnection))
        {
            await source.OpenAsync(ct);
            // Use the repository's actual SS EF schema, then clear its bundled reference
            // seeds only in this newly created source to make the one-row assertion exact.
            var schemaPath = FindSourceSchema();
            await using var schema = new NpgsqlCommand(await File.ReadAllTextAsync(schemaPath, ct), source)
            {
                CommandTimeout = 120,
            };
            await schema.ExecuteNonQueryAsync(ct);
            await using var fixture = new NpgsqlCommand(
                """
                TRUNCATE "Region", "Location", "LookupCode", "LookupSortOrder", "Role",
                    "User", "UserRole", "SheriffActingRank", "SheriffAwayLocation", "SheriffLeave" CASCADE;
                INSERT INTO "Region" ("Id", "JustinId", "Name", "CreatedOn")
                VALUES (900001, 900001, 'Synthetic Apply Region', '2020-01-01T00:00:00Z');
                """,
                source
            );
            await fixture.ExecuteNonQueryAsync(ct);
        }

        options.Sources.SS.ConnectionString = sourceConnection;
        Assert.True(options.Sources.SS.Jobs.MyTeam.DryRun);
        Assert.True(options.Sources.SS.Jobs.MyTeam.AllowMigrationApply);
        Assert.False(options.Sources.SS.AllowUnrecoverableSyntheticApply);
        Assert.StartsWith("migration_it_", database); // Not in the rehearsal-only allowlist.
        var (operations, dispatcher) = Operations(db);
        foreach (var jsonRequest in new[] { "{}", """{"dryRun":true}""" })
        {
            var request = JsonSerializer.Deserialize<MigrationTriggerRequest>(
                jsonRequest,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
            )!;
            Assert.True(request.DryRun);
            var dry = await operations.TriggerAsync("SS", "MyTeam", request.DryRun, actor, ct);
            Assert.True(dry.DryRun);
            Assert.Equal(dry.RunId, dispatcher.Enqueued);
            db.ChangeTracker.Clear();
            var audit = await db.AuditRecords.AsNoTracking().SingleAsync(x => x.EntityPK == dry.RunId.ToString(), ct);
            Assert.Equal(actor, audit.ActorUserId);
            using var auditJson = JsonDocument.Parse(audit.NewValues!);
            Assert.True(auditJson.RootElement.GetProperty("DryRun").GetBoolean());
            await using var simulationDb = NewDb();
            await Executor(simulationDb).ExecuteAcquiredAsync(dry.RunId, ct);
            var simulated = await simulationDb.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == dry.RunId, ct);
            Assert.True(simulated.DryRun);
            Assert.Equal("Completed", simulated.Status);
            Assert.Equal(1, simulated.ScannedCount);
            Assert.Equal(1, simulated.InsertedCount);
            Assert.Equal(0, simulated.RejectedCount);
            Assert.Equal(0, simulated.FailedCount);
            Assert.Empty(await simulationDb.Regions.AsNoTracking().ToListAsync(ct));
            Assert.Empty(await simulationDb.DataMigrationRecords.AsNoTracking().ToListAsync(ct));
            var candidate = await simulationDb
                .DataMigrationSimulationCandidates.AsNoTracking()
                .SingleAsync(x => x.RunId == dry.RunId, ct);
            Assert.Equal("Region", candidate.EntityType);
            Assert.Equal("900001", candidate.LegacyId);
            Assert.True(
                await simulationDb.DataMigrationSimulations.AsNoTracking().AnyAsync(x => x.RunId == dry.RunId, ct)
            );
            Assert.Empty(await simulationDb.DataMigrationFailures.AsNoTracking().ToListAsync(ct));
            db.ChangeTracker.Clear();
        }

        var accepted = await operations.TriggerAsync("SS", "MyTeam", false, actor, ct);
        Assert.False(accepted.DryRun);
        Assert.Equal(accepted.RunId, dispatcher.Enqueued);
        await using var workerDb = NewDb();
        await Executor(workerDb).ExecuteAcquiredAsync(accepted.RunId, ct);
        var run = await workerDb.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == accepted.RunId, ct);
        Assert.False(run.DryRun);
        Assert.Equal("Completed", run.Status);
        Assert.Equal(1, run.ScannedCount);
        Assert.Equal(1, run.InsertedCount);
        Assert.Equal(0, run.RejectedCount);
        Assert.Equal(0, run.FailedCount);
        Assert.Empty(await workerDb.DataMigrationFailures.AsNoTracking().ToListAsync(ct));
        var crosswalk = await workerDb.DataMigrationRecords.AsNoTracking().SingleAsync(ct);
        Assert.Equal("SS", crosswalk.Source);
        Assert.Equal("Region", crosswalk.EntityType);
        Assert.Equal("900001", crosswalk.LegacyId);
        Assert.Equal("Region", crosswalk.TargetType);
        Assert.Equal(accepted.RunId, crosswalk.CreatedByRunId);
        var region = await workerDb.Regions.AsNoTracking().SingleAsync(x => x.Id == int.Parse(crosswalk.TargetKey), ct);
        Assert.Equal("Synthetic Apply Region", region.Name);
        Assert.Equal(900001, region.JustinId);
        var outcome = await workerDb
            .DataMigrationOutcomes.AsNoTracking()
            .SingleAsync(x => x.RunId == accepted.RunId, ct);
        Assert.Equal("Migrated", outcome.Outcome);

        var rollbackConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DataMigration:Sources:SS:AllowUnrecoverableSyntheticApply"] = "true",
                }
            )
            .Build();
        var rollback = new SsRollbackService(workerDb, new SsRollbackJournal(workerDb, rollbackConfig), rollbackConfig);
        var beforeRuns = await workerDb
            .DataMigrationRuns.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Status,
                x.FailedCount,
            })
            .ToListAsync(ct);
        var mutationCount = await workerDb.DataMigrationMutations.CountAsync(ct);
        var outcomeCount = await workerDb.DataMigrationOutcomes.CountAsync(ct);
        var auditCount = await workerDb.AuditRecords.CountAsync(ct);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rollback.PreviewAsync(accepted.RunId, ct)
        );
        Assert.Equal("SS rollback apply/preview remains isolated-rehearsal only.", error.Message);
        error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rollback.ApplyAsync(accepted.RunId, "unused", ct)
        );
        Assert.Equal("SS rollback apply/preview remains isolated-rehearsal only.", error.Message);
        var afterRuns = await workerDb
            .DataMigrationRuns.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Status,
                x.FailedCount,
            })
            .ToListAsync(ct);
        Assert.Equal(beforeRuns, afterRuns);
        Assert.Equal(mutationCount, await workerDb.DataMigrationMutations.CountAsync(ct));
        Assert.Equal(outcomeCount, await workerDb.DataMigrationOutcomes.CountAsync(ct));
        Assert.Equal(auditCount, await workerDb.AuditRecords.CountAsync(ct));
        Assert.Empty(await workerDb.DataMigrationFailures.AsNoTracking().ToListAsync(ct));
        var retained = await workerDb.DataMigrationRecords.AsNoTracking().SingleAsync(ct);
        Assert.Equal(crosswalk.Id, retained.Id);
        Assert.Equal(crosswalk.TargetKey, retained.TargetKey);
        Assert.Equal(crosswalk.PayloadHash, retained.PayloadHash);
        Assert.Equal("Synthetic Apply Region", (await workerDb.Regions.AsNoTracking().SingleAsync(ct)).Name);
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

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsConfigured))]
    public async Task AuditedTrigger_PersistedAuditAllowsWorkerToReachSourceFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var (operations, dispatcher) = Operations(db);
        var accepted = await operations.TriggerAsync("SS", "MyTeam", false, actor, ct);
        Assert.Equal(accepted.RunId, dispatcher.Enqueued);
        db.ChangeTracker.Clear();
        var audit = await db.AuditRecords.AsNoTracking().SingleAsync(ct);
        Assert.Equal(actor, audit.ActorUserId);
        using var json = JsonDocument.Parse(audit.NewValues!);
        Assert.Equal(JsonValueKind.False, json.RootElement.GetProperty("DryRun").ValueKind);
        await using var workerDb = NewDb();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Executor(workerDb).ExecuteAcquiredAsync(accepted.RunId, ct)
        );
        var run = await workerDb.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == accepted.RunId, ct);
        Assert.Equal("Failed", run.Status);
        Assert.Equal(1, run.FailedCount);
        Assert.Equal(
            "ExecutionFailed",
            (await workerDb.DataMigrationFailures.AsNoTracking().SingleAsync(ct)).ErrorType
        );
        Assert.Empty(await workerDb.DataMigrationRecords.ToListAsync(ct));
    }

    [Theory(Skip = SkipReason, SkipUnless = nameof(IsConfigured))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Worker_RejectsMissingOrMismatchedPersistedAudit(bool mismatch)
    {
        var ct = TestContext.Current.CancellationToken;
        var run = await new DataMigrationControlService(db).TryStartJobAsync(
            "MyTeam",
            false,
            options.Sources.SS.CutoverDate!.Value,
            ct,
            options.Sources.SS.HistoryStartDate
        );
        Assert.NotNull(run);
        if (mismatch)
        {
            db.AuditRecords.Add(
                new AuditRecord
                {
                    ActorUserId = actor,
                    Action = "Added",
                    EntityType = "DataMigrationOperation",
                    EntityPK = run.Id.ToString("D"),
                    TableName = "DataMigrationRuns",
                    OccurredOn = DateTimeOffset.UtcNow,
                    NewValues = JsonSerializer.Serialize(
                        new
                        {
                            Source = "SS",
                            Job = "Training",
                            RunId = run.Id,
                            Operation = "Trigger",
                            DryRun = false,
                        }
                    ),
                }
            );
            await db.SaveChangesAsync(ct);
        }
        await using var workerDb = NewDb();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Executor(workerDb).ExecuteAcquiredAsync(run.Id, ct));
        Assert.Equal(
            "QueuedExecutionRefused",
            (await workerDb.DataMigrationFailures.AsNoTracking().SingleAsync(ct)).ErrorType
        );
        var saved = await workerDb.DataMigrationRuns.AsNoTracking().SingleAsync(ct);
        Assert.Equal("Failed", saved.Status);
        Assert.Equal(1, saved.FailedCount);
        Assert.Empty(await workerDb.DataMigrationRecords.ToListAsync(ct));
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsConfigured))]
    public async Task Worker_RejectsRevokedOptInAfterAuditedTrigger()
    {
        var ct = TestContext.Current.CancellationToken;
        var (operations, _) = Operations(db);
        var accepted = await operations.TriggerAsync("SS", "MyTeam", false, actor, ct);
        options.Sources.SS.Jobs.MyTeam.AllowMigrationApply = false;
        await using var workerDb = NewDb();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Executor(workerDb).ExecuteAcquiredAsync(accepted.RunId, ct)
        );
        Assert.Equal(
            "QueuedExecutionRefused",
            (await workerDb.DataMigrationFailures.AsNoTracking().SingleAsync(ct)).ErrorType
        );
        Assert.Equal("Failed", (await workerDb.DataMigrationRuns.AsNoTracking().SingleAsync(ct)).Status);
        Assert.Empty(await workerDb.DataMigrationRecords.ToListAsync(ct));
    }

    [Theory(Skip = SkipReason, SkipUnless = nameof(IsConfigured))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReportTechnicalError_IsNeedsActionWithoutChangingLoaders(bool dispatchFailure)
    {
        var ct = TestContext.Current.CancellationToken;
        var parent = Completed("MyTeam", null);
        var training = Completed("Training", parent.Id);
        var scheduling = Completed("Scheduling", parent.Id);
        db.DataMigrationRuns.AddRange(parent, training, scheduling);
        await db.SaveChangesAsync(ct);
        var (operations, dispatcher) = Operations(db);
        dispatcher.Fail = dispatchFailure;
        Guid reportId;
        if (dispatchFailure)
        {
            var error = await Assert.ThrowsAsync<MigrationOperationException>(() =>
                operations.TriggerAsync("SS", "Report", false, actor, ct)
            );
            Assert.Equal("JobDispatchFailed", error.Code);
            reportId = (await db.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Job == "Report", ct)).Id;
        }
        else
        {
            var accepted = await operations.TriggerAsync("SS", "Report", false, actor, ct);
            reportId = accepted.RunId;
            await using var workerDb = NewDb();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Executor(workerDb).ExecuteAcquiredAsync(reportId, ct)
            );
        }
        db.ChangeTracker.Clear();
        var report = await db.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == reportId, ct);
        Assert.Equal("NeedsAction", report.Status);
        Assert.Equal(0, report.FailedCount);
        Assert.Equal(0, report.RejectedCount);
        var failure = await db.DataMigrationFailures.AsNoTracking().SingleAsync(x => x.RunId == reportId, ct);
        Assert.Equal(dispatchFailure ? "DispatchFailed" : "ExecutionFailed", failure.ErrorType);
        Assert.DoesNotContain("synthetic", failure.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        var dto = await operations.RunAsync("SS", "Report", reportId, ct);
        Assert.Equal("NeedsAction", dto.Verdict);
        var findings = await operations.FindingsAsync("SS", "Report", reportId, "failures", 0, 10, ct);
        Assert.Equal("NeedsAction", Assert.Single(findings.Items).Outcome);
        var loaders = await db.DataMigrationRuns.AsNoTracking().Where(x => x.Job != "Report").ToListAsync(ct);
        Assert.Equal(3, loaders.Count);
        Assert.All(
            loaders,
            x =>
            {
                Assert.Equal("Completed", x.Status);
                Assert.Equal(0, x.FailedCount);
            }
        );
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsConfigured))]
    public async Task RollbackJournal_UsesIndexedTypedKeysAndExactSavepointXmin()
    {
        var ct = TestContext.Current.CancellationToken;
        var run = Completed("MyTeam", null);
        db.DataMigrationRuns.Add(run);
        await db.SaveChangesAsync(ct);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        async Task Execute(string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(ct);
        }
        async Task<string> Scalar(string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection);
            return (string)(await command.ExecuteScalarAsync(ct))!;
        }
        await Execute(
            """
            CREATE TABLE public."SsVersionProbe" ("Part Id" int, "Key" text, "Value" text,
              PRIMARY KEY ("Part Id","Key"));
            INSERT INTO public."SsVersionProbe"
              SELECT i, 'synthetic', 'before' FROM generate_series(1,98000) i;
            ANALYZE public."SsVersionProbe";
            CREATE TRIGGER ss_rollback_capture AFTER INSERT OR UPDATE OR DELETE ON public."SsVersionProbe"
              FOR EACH ROW EXECUTE FUNCTION public.ss_capture_rollback('["Part Id","Key"]');
            """
        );
        var query = """
            SELECT t.xmin::text FROM public."SsVersionProbe" t
            CROSS JOIN jsonb_populate_record(NULL::public."SsVersionProbe",'{"Part Id":42000,"Key":"synthetic"}') k
            WHERE t."Part Id" = k."Part Id" AND t."Key" = k."Key"
            """;
        var plan = await Scalar("EXPLAIN (FORMAT JSON) " + query);
        Assert.Contains("Index Scan", plan);
        Assert.Contains("SsVersionProbe_pkey", plan);
        Assert.DoesNotContain("Seq Scan", plan);
        var definition = await Scalar("SELECT pg_get_functiondef('public.ss_capture_rollback()'::regprocedure)");
        Assert.Contains("jsonb_populate_record", definition);
        Assert.DoesNotContain("WHERE to_jsonb(t)", definition);
        // Settings are synthetic and transaction-local, never applied to a live resource.
        await Execute(
            $"""
            BEGIN;
            SET LOCAL unified_ss.run = '{run.Id}';
            SET LOCAL unified_ss.key_id = '{Guid.NewGuid()}';
            SET LOCAL unified_ss.key = 'synthetic-only';
            SAVEPOINT inserted;
            INSERT INTO public."SsVersionProbe" VALUES (99000,'inserted','new');
            """
        );
        async Task ExactVersion(string table, string predicate)
        {
            var actual = await Scalar($"SELECT xmin::text FROM public.\"{table}\" WHERE {predicate}");
            var captured = await Scalar(
                $"""
                SELECT "AfterVersion" FROM public."DataMigrationJournals"
                WHERE "RunId"='{run.Id}' AND "TableName"='{table}' ORDER BY "Id" DESC LIMIT 1
                """
            );
            Assert.Equal(actual, captured);
            Assert.NotEqual(await Scalar("SELECT (pg_current_xact_id()::text::numeric % 4294967296)::text"), captured);
        }
        await ExactVersion("SsVersionProbe", "\"Part Id\"=99000 AND \"Key\"='inserted'");
        await Execute(
            """
            RELEASE SAVEPOINT inserted;
            SAVEPOINT updated;
            UPDATE public."SsVersionProbe" SET "Value"='updated' WHERE "Part Id"=42000 AND "Key"='synthetic';
            """
        );
        await ExactVersion("SsVersionProbe", "\"Part Id\"=42000 AND \"Key\"='synthetic'");
        await Execute(
            """
            SAVEPOINT reverted;
            UPDATE public."SsVersionProbe" SET "Value"='reverted' WHERE "Part Id"=42000 AND "Key"='synthetic';
            ROLLBACK TO SAVEPOINT reverted;
            RELEASE SAVEPOINT reverted;
            """
        );
        await ExactVersion("SsVersionProbe", "\"Part Id\"=42000 AND \"Key\"='synthetic'");
        await Execute(
            """
            RELEASE SAVEPOINT updated;
            SAVEPOINT singlekey;
            INSERT INTO public."Regions" ("Id","Name","CreatedOn") VALUES (99000,'Synthetic',now());
            """
        );
        await ExactVersion("Regions", "\"Id\"=99000");
        await Execute(
            """
            RELEASE SAVEPOINT singlekey;
            DELETE FROM public."SsVersionProbe" WHERE "Part Id"=99000 AND "Key"='inserted';
            """
        );
        Assert.Equal(
            "missing",
            await Scalar(
                $"""
                SELECT "AfterVersion" FROM public."DataMigrationJournals"
                WHERE "RunId"='{run.Id}' ORDER BY "Id" DESC LIMIT 1
                """
            )
        );
        await Execute("COMMIT");
        // No migration run setting: ordinary DML still produces no journal.
        await Execute("""UPDATE public."SsVersionProbe" SET "Value"='ordinary' WHERE "Part Id"=1""");
        Assert.Equal(
            "4",
            await Scalar(
                $"""
                SELECT count(*)::text FROM public."DataMigrationJournals" WHERE "RunId"='{run.Id}'
                """
            )
        );
    }

    private DataMigrationRun Completed(string job, Guid? parent) =>
        new()
        {
            Source = "SS",
            Job = job,
            MyTeamRunId = parent,
            Status = "Completed",
            DryRun = false,
            StartedOn = DateTimeOffset.UtcNow.AddMinutes(-5),
            CompletedOn = DateTimeOffset.UtcNow.AddMinutes(-4),
            CutoverDate = options.Sources.SS.CutoverDate,
            HistoryStartDate = options.Sources.SS.HistoryStartDate,
        };

    private sealed class Dispatcher(SsMigrationExecutor executor) : IMigrationJobDispatcher
    {
        public bool Fail { get; set; }
        public Guid? Enqueued { get; private set; }

        public void Validate(string job, bool dryRun) => executor.ValidateRequest(job, dryRun);

        public void Enqueue(Guid runId)
        {
            if (Fail)
                throw new InvalidOperationException("synthetic private detail");
            Enqueued = runId;
        }
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

    public async ValueTask DisposeAsync()
    {
        if (db is not null)
            await db.DisposeAsync();
        if (!created && !sourceCreated)
            return;
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(Admin) { Pooling = false }.ConnectionString
        );
        await connection.OpenAsync(CancellationToken.None);
        foreach (var name in new[] { created ? database : null, sourceCreated ? sourceDatabase : null })
        {
            if (name is null)
                continue;
            await using var command = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
