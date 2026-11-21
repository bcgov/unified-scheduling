using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Unified.Authorization.Claims;
using Unified.DataMigration.Controllers;
using Unified.DataMigration.FeatureFlags;
using Unified.DataMigration.Jobs;
using Unified.DataMigration.Models;
using Unified.DataMigration.Options;
using Unified.DataMigration.Services;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.DataMigration;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.DataMigration;

public sealed class MigrationApplySafetyTests : IAsyncDisposable
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly UnifiedDbContext db;
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

    public MigrationApplySafetyTests()
    {
        connection.Open();
        db = new SqliteTestUnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(connection).Options
        );
        db.Database.EnsureCreated();
        options.Sources.SS.Enabled = true;
        options.Sources.SS.ConnectionString = "not-used";
        options.Sources.SS.CutoverDate = new DateOnly(2026, 12, 1);
        foreach (var job in DataMigrationOperations.Jobs)
            options.Sources.SS.Jobs.For(job).Enabled = true;
    }

    private SsMigrationExecutor Executor() =>
        SsMigrationTestServices.Executor(db, Options.Create(options), flags, configuration);

    [Theory]
    [InlineData("MyTeam")]
    [InlineData("Training")]
    [InlineData("Scheduling")]
    [InlineData("Report")]
    public void Apply_RequiresSelectedJobFlag_NotConfiguredDryRunOrDatabaseAllowlist(string job)
    {
        var executor = Executor();
        Assert.True(options.Sources.SS.Jobs.For(job).DryRun);
        Assert.False(options.Sources.SS.Jobs.For(job).AllowMigrationApply);
        Assert.Throws<InvalidOperationException>(() => executor.ValidateRequest(job, false));
        options.Sources.SS.Jobs.For(job).AllowMigrationApply = true;
        Assert.False(executor.ValidateRequest(job, false).DryRun);
        Assert.True(executor.ValidateRequest(job, true).DryRun);
        Assert.True(JsonSerializer.Deserialize<MigrationTriggerRequest>("{}")!.DryRun);
        Assert.False(
            JsonSerializer
                .Deserialize<MigrationTriggerRequest>(
                    "{\"dryRun\":false}",
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)
                )!
                .DryRun
        );
    }

    [Fact]
    public async Task DirectAndRecurringApply_AreRefusedBeforeAcquisition()
    {
        options.Sources.SS.Jobs.MyTeam.DryRun = false;
        options.Sources.SS.Jobs.MyTeam.AllowMigrationApply = true;
        var executor = Executor();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync("MyTeam", CancellationToken.None)
        );
        var recurring = new SsDataMigrationRecurringJob(flags, Options.Create(options), executor);
        await Assert.ThrowsAsync<InvalidOperationException>(() => recurring.Execute(null, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ValidateExecutionAsync("MyTeam", null, CancellationToken.None)
        );
        Assert.Empty(db.DataMigrationRuns);
        options.Sources.SS.Jobs.MyTeam.DryRun = true;
        await executor.ValidateExecutionAsync("MyTeam", null, CancellationToken.None);
    }

    [Fact]
    public async Task AcquiredApply_RechecksAuditFlagModeAndConfiguration()
    {
        var job = options.Sources.SS.Jobs.MyTeam;
        job.AllowMigrationApply = true;
        var run = new DataMigrationRun
        {
            Source = "SS",
            Job = "MyTeam",
            Status = "Running",
            DryRun = false,
            CutoverDate = options.Sources.SS.CutoverDate,
            HistoryStartDate = options.Sources.SS.HistoryStartDate,
        };
        var executor = Executor();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ValidateExecutionAsync("MyTeam", run, CancellationToken.None)
        );
        var audit = new AuditRecord
        {
            ActorUserId = Guid.NewGuid(),
            Action = "Added",
            EntityType = "DataMigrationOperation",
            EntityPK = run.Id.ToString("D"),
            TableName = "DataMigrationRuns",
            NewValues = JsonSerializer.Serialize(
                new
                {
                    Source = "SS",
                    Job = "Training",
                    RunId = run.Id,
                    Operation = "Trigger",
                }
            ),
        };
        db.AuditRecords.Add(audit);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ValidateExecutionAsync("MyTeam", run, CancellationToken.None)
        );
        audit.NewValues = JsonSerializer.Serialize(
            new
            {
                Source = "SS",
                Job = "MyTeam",
                RunId = run.Id,
                Operation = "Trigger",
            }
        );
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ValidateExecutionAsync("MyTeam", run, CancellationToken.None)
        );
        audit.NewValues = JsonSerializer.Serialize(
            new
            {
                Source = "SS",
                Job = "MyTeam",
                RunId = run.Id,
                Operation = "Trigger",
                DryRun = true,
            }
        );
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ValidateExecutionAsync("MyTeam", run, CancellationToken.None)
        );
        audit.NewValues = JsonSerializer.Serialize(
            new
            {
                Source = "SS",
                Job = "MyTeam",
                RunId = run.Id,
                Operation = "Trigger",
                DryRun = false,
            }
        );
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await executor.ValidateExecutionAsync("MyTeam", run, CancellationToken.None);
        job.AllowMigrationApply = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ValidateExecutionAsync("MyTeam", run, CancellationToken.None)
        );
        run.DryRun = true;
        await executor.ValidateExecutionAsync("MyTeam", run, CancellationToken.None);
        run.HistoryStartDate = new DateOnly(2019, 1, 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ValidateExecutionAsync("MyTeam", run, CancellationToken.None)
        );
        run.HistoryStartDate = options.Sources.SS.HistoryStartDate;
        options.Sources.SS.Jobs.MyTeam.Enabled = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ValidateExecutionAsync("MyTeam", run, CancellationToken.None)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Trigger_RequiresAuthenticatedAuthorizedMatchingActor(bool dryRun)
    {
        var coordinator = new Coordinator(db);
        var dispatcher = new Dispatcher(Executor());
        var context = new HttpContextAccessor();
        var auth = new Authorization { Allowed = true };
        var operations = Operations(coordinator, dispatcher, context, auth);
        var actor = Guid.NewGuid();
        options.Sources.SS.Jobs.MyTeam.AllowMigrationApply = true;
        await Assert.ThrowsAsync<MigrationOperationException>(() =>
            operations.TriggerAsync("SS", "MyTeam", dryRun, actor, CancellationToken.None)
        );
        foreach (var claim in new[] { "", "not-a-guid", actor.ToString() })
        {
            context.HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        [new Claim(UnifiedClaimTypes.UserId, claim)],
                        claim == actor.ToString() ? null : "test"
                    )
                ),
            };
            await Assert.ThrowsAsync<MigrationOperationException>(() =>
                operations.TriggerAsync("SS", "MyTeam", dryRun, actor, CancellationToken.None)
            );
        }
        context.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(UnifiedClaimTypes.UserId, actor.ToString())], "test")
            ),
        };
        auth.Allowed = false;
        await Assert.ThrowsAsync<MigrationOperationException>(() =>
            operations.TriggerAsync("SS", "MyTeam", dryRun, actor, CancellationToken.None)
        );
        auth.Allowed = true;
        await Assert.ThrowsAsync<MigrationOperationException>(() =>
            operations.TriggerAsync("SS", "MyTeam", dryRun, Guid.NewGuid(), CancellationToken.None)
        );
        await Assert.ThrowsAsync<MigrationOperationException>(() =>
            operations.TriggerAsync("SS", "MyTeam", dryRun, Guid.Empty, CancellationToken.None)
        );
        Assert.Empty(db.DataMigrationRuns);
        Assert.Empty(db.AuditRecords);
        Assert.Null(dispatcher.Enqueued);
        var accepted = await operations.TriggerAsync("SS", "MyTeam", dryRun, actor, CancellationToken.None);
        Assert.Equal(accepted.RunId, dispatcher.Enqueued);
        var run = await db.DataMigrationRuns.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(dryRun, run.DryRun);
        await Executor().ValidateExecutionAsync("MyTeam", run, CancellationToken.None);
        Assert.Equal(actor, (await db.AuditRecords.SingleAsync(TestContext.Current.CancellationToken)).ActorUserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/scheduling")]
    [InlineData("/tenant/scheduling")]
    public async Task TriggerAcceptedLocation_PreservesRequestPathBase(string pathBase)
    {
        var actor = Guid.NewGuid();
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(UnifiedClaimTypes.UserId, actor.ToString())], "test")
            ),
        };
        http.Request.PathBase = pathBase;
        var dispatcher = new Dispatcher(Executor());
        var operations = Operations(
            new Coordinator(db),
            dispatcher,
            new HttpContextAccessor { HttpContext = http },
            new Authorization { Allowed = true }
        );
        var controller = new DataMigrationController(operations)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };

        var result = Assert.IsType<AcceptedResult>(
            await controller.Trigger(
                "SS",
                "MyTeam",
                new MigrationTriggerRequest(),
                TestContext.Current.CancellationToken
            )
        );

        var accepted = Assert.IsType<MigrationAcceptedDto>(result.Value);
        Assert.Equal($"{pathBase}/api/data-migration/SS/jobs/MyTeam/runs/{accepted.RunId:D}", result.Location);
        Assert.Equal(accepted.RunId, dispatcher.Enqueued);
        Assert.Equal(
            accepted.RunId,
            (await db.DataMigrationRuns.SingleAsync(TestContext.Current.CancellationToken)).Id
        );
    }

    [Theory]
    [InlineData("Report", "NeedsAction", 0)]
    [InlineData("MyTeam", "Failed", 1)]
    public async Task DispatchError_SeparatesReportFromLoader(string job, string expected, int failures)
    {
        var dispatcher = new Dispatcher(Executor()) { Fail = true };
        var actor = Guid.NewGuid();
        var context = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity([new Claim(UnifiedClaimTypes.UserId, actor.ToString())], "test")
                ),
            },
        };
        var operations = Operations(new Coordinator(db), dispatcher, context, new Authorization { Allowed = true });
        var ex = await Assert.ThrowsAsync<MigrationOperationException>(() =>
            operations.TriggerAsync("SS", job, true, actor, CancellationToken.None)
        );
        Assert.Equal("JobDispatchFailed", ex.Code);
        var run = await db.DataMigrationRuns.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expected, run.Status);
        Assert.Equal(failures, run.FailedCount);
        var findings = await operations.FindingsAsync("SS", job, run.Id, "failures", 0, 10, CancellationToken.None);
        Assert.Equal(job == "Report" ? "NeedsAction" : "Failed", Assert.Single(findings.Items).Outcome);
        Assert.Equal("DispatchFailed", Assert.Single(findings.Items).Code);
    }

    [Theory]
    [InlineData("Report", 0, 0, "NeedsAction", 0)]
    [InlineData("Report", 1, 0, "Failed", 1)]
    [InlineData("Report", 0, 1, "Failed", 0)]
    [InlineData("Training", 0, 0, "Failed", 1)]
    public void WorkerTechnicalError_PreservesRealDataFailures(
        string job,
        int failed,
        int rejected,
        string status,
        int count
    )
    {
        var run = new DataMigrationRun
        {
            Source = "SS",
            Job = job,
            Status = "Running",
            FailedCount = failed,
            RejectedCount = rejected,
        };
        MigrationTechnicalFailure.Record(db, run, "ExecutionFailed", "Safe diagnostic.");
        Assert.Equal(status, run.Status);
        Assert.Equal(count, run.FailedCount);
        Assert.Equal(rejected, run.RejectedCount);
    }

    [Fact]
    public void TechnicalFailureDescription_KeepsOriginalCauseWithoutRowDetail()
    {
        var pg = new Npgsql.PostgresException(
            "duplicate key value violates unique constraint",
            "ERROR",
            "ERROR",
            "23505",
            detail: "Key (LegacyId)=(secret-row) already exists.",
            tableName: "DataMigrationRecords",
            constraintName: "IX_DataMigrationRecords_Source_EntityType_LegacyId"
        );
        var ex = new DbUpdateException("An error occurred while saving the entity changes.", pg);

        var text = MigrationTechnicalFailure.Describe(ex);

        Assert.Contains("Microsoft.EntityFrameworkCore.DbUpdateException", text);
        Assert.Contains("Npgsql.PostgresException", text);
        Assert.Contains("SqlState=23505", text);
        Assert.Contains("Constraint=IX_DataMigrationRecords_Source_EntityType_LegacyId", text);
        Assert.Contains("Table=DataMigrationRecords", text);
        Assert.DoesNotContain("secret-row", text);
        Assert.True(
            MigrationTechnicalFailure.Describe(new InvalidOperationException(new string('x', 5000))).Length
                <= MigrationTechnicalFailure.MaxDescriptionLength
        );
    }

    [Fact]
    public async Task ReportVerdict_DoesNotHideActualFailureOrPassTechnicalError()
    {
        var run = new DataMigrationRun
        {
            Source = "SS",
            Job = "Report",
            Status = "Completed",
            DryRun = false,
        };
        db.DataMigrationRuns.Add(run);
        db.DataMigrationFailures.Add(
            new DataMigrationFailure
            {
                RunId = run.Id,
                Source = "SS",
                ErrorType = "ExecutionFailed",
                ErrorMessage = "Safe diagnostic.",
                OccurredOn = DateTimeOffset.UtcNow,
            }
        );
        db.DataMigrationOutcomes.Add(
            new DataMigrationOutcome
            {
                RunId = run.Id,
                EntityType = "SourceOracleValidation",
                LegacyId = run.Id.ToString(),
                Outcome = "Migrated",
                Reason = "ActualIndependentOracleRowsMatched",
            }
        );
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var operations = Operations(
            new Coordinator(db),
            new Dispatcher(Executor()),
            new HttpContextAccessor(),
            new Authorization()
        );
        var result = await operations.RunAsync("SS", "Report", run.Id, CancellationToken.None);
        Assert.Equal("NeedsAction", result.Verdict);
        db.DataMigrationOutcomes.Add(
            new DataMigrationOutcome
            {
                RunId = run.Id,
                EntityType = "User",
                LegacyId = "1",
                Outcome = "Failed",
                Reason = "MissingPrimaryCrosswalk",
            }
        );
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        result = await operations.RunAsync("SS", "Report", run.Id, CancellationToken.None);
        Assert.Equal("Fail", result.Verdict);
    }

    private DataMigrationOperations Operations(
        Coordinator coordinator,
        Dispatcher dispatcher,
        IHttpContextAccessor context,
        IAuthorizationService authorization
    ) => new(db, Options.Create(options), flags, coordinator, dispatcher, null!, context, authorization);

    private sealed class Authorization : IAuthorizationService
    {
        public bool Allowed { get; set; }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            IEnumerable<IAuthorizationRequirement> requirements
        ) => Task.FromResult(Allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName) =>
            Task.FromResult(Allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
    }

    private sealed class Dispatcher(SsMigrationExecutor executor) : IMigrationJobDispatcher
    {
        public bool Fail { get; set; }
        public Guid? Enqueued { get; private set; }

        public void Validate(string job, bool dryRun) => executor.ValidateRequest(job, dryRun);

        public void Enqueue(Guid runId)
        {
            if (Fail)
                throw new InvalidOperationException("private credential");
            Enqueued = runId;
        }
    }

    private sealed class Coordinator(UnifiedDbContext db) : IMigrationRunCoordinator
    {
        public async Task<DataMigrationRun?> TryStartJobAsync(
            string job,
            bool dryRun,
            DateOnly cutover,
            CancellationToken ct,
            DateOnly? historyStartDate = null
        )
        {
            var run = new DataMigrationRun
            {
                Source = "SS",
                Job = job,
                Status = "Running",
                DryRun = dryRun,
                CutoverDate = cutover,
                HistoryStartDate = historyStartDate,
            };
            db.DataMigrationRuns.Add(run);
            await db.SaveChangesAsync(ct);
            return run;
        }

        public Task<bool> RecoverStaleRunAsync(Guid runId, DateTimeOffset staleBefore, CancellationToken ct) =>
            Task.FromResult(false);
    }

    public async ValueTask DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }
}
