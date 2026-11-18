using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Common.Mvc;
using Unified.DataMigration;
using Unified.DataMigration.Controllers;
using Unified.DataMigration.Models;
using Unified.DataMigration.Services;
using Unified.Db;
using Unified.Db.Models.DataMigration;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.DataMigration;

public sealed class MigrationOperationsTests
{
    [Theory]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("none", HttpStatusCode.Forbidden)]
    [InlineData("rollback-only", HttpStatusCode.Forbidden)]
    [InlineData("unresolved", HttpStatusCode.Forbidden)]
    [InlineData("operate", HttpStatusCode.OK)]
    public async Task HttpRead_UsesExistingAuthenticatedPermissionAndResolvedActor(string mode, HttpStatusCode expected)
    {
        await using var test = await Harness.Start();
        using var request = Request(mode, HttpMethod.Get, "/api/data-migration/SS/jobs/MyTeam/status");
        using var response = await test.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("SS", "Training", HttpStatusCode.NotFound)]
    [InlineData("CASS", "MyTeam", HttpStatusCode.NotImplemented)]
    [InlineData("OTHER", "MyTeam", HttpStatusCode.NotFound)]
    [InlineData("SS", "Unknown", HttpStatusCode.NotFound)]
    public async Task HttpRunAndExport_RejectSourceJobConfusion(string source, string job, HttpStatusCode expected)
    {
        await using var test = await Harness.Start();
        var id = await test.Seed();
        foreach (
            var suffix in new[]
            {
                "",
                "/findings/outcomes",
                "/export/csv",
                "/rollback-impact?digest=" + new string('A', 64),
            }
        )
        {
            using var request = Request(
                "both",
                HttpMethod.Get,
                $"/api/data-migration/{source}/jobs/{job}/runs/{id}{suffix}"
            );
            using var response = await test.Client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(expected, response.StatusCode);
            Assert.DoesNotContain(
                "private",
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
            );
        }
        Assert.Empty(await test.Db.AuditRecords.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("rollback-preview", "POST")]
    [InlineData("rollback-impact?digest=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "GET")]
    [InlineData("rollback", "POST")]
    public async Task RestrictedRollback_RequiresAdditionalPermissionBeforeServiceOrAudit(string suffix, string method)
    {
        await using var test = await Harness.Start();
        var id = await test.Seed();
        using var request = Request(
            "operate",
            new HttpMethod(method),
            $"/api/data-migration/SS/jobs/MyTeam/runs/{id}/{suffix}"
        );
        if (method == "POST")
            request.Content = JsonContent.Create(new { ConfirmDataLoss = id, Digest = new string('A', 64) });
        using var response = await test.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await test.Db.AuditRecords.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RollbackConfirmation_IsBoundToRouteRunBeforeAnyInverseOrSensitiveExport()
    {
        await using var test = await Harness.Start();
        var id = await test.Seed();
        foreach (
            var payload in new[]
            {
                new MigrationRollbackRequest(Guid.NewGuid(), new string('A', 64)),
                new MigrationRollbackRequest(id, "../private"),
            }
        )
        {
            using var request = Request(
                "both",
                HttpMethod.Post,
                $"/api/data-migration/SS/jobs/MyTeam/runs/{id}/rollback"
            );
            request.Content = JsonContent.Create(payload);
            using var response = await test.Client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        Assert.Empty(await test.Db.AuditRecords.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            "Failed",
            (await test.Db.DataMigrationRuns.SingleAsync(x => x.Id == id, TestContext.Current.CancellationToken)).Status
        );
    }

    [Fact]
    public async Task Trigger_DefaultsDryRun_ReturnsCoordinatorRunId_AndDispatchesExactlyThatId()
    {
        await using var test = await Harness.Start();
        using var request = Request("operate", HttpMethod.Post, "/api/data-migration/SS/jobs/MyTeam/runs");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var response = await test.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var value = await response.Content.ReadFromJsonAsync<MigrationAcceptedDto>(
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(value);
        Assert.True(value.DryRun);
        Assert.Equal(test.Dispatcher.Enqueued, value.RunId);
        Assert.EndsWith(value.RunId.ToString(), response.Headers.Location!.ToString());
        var run = await test.Db.DataMigrationRuns.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(value.RunId, run.Id);
        Assert.True(run.DryRun);
        Assert.Equal(1, test.Coordinator.Starts);
        Assert.Single(await test.Db.AuditRecords.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FailedDispatch_PersistsStableFailedRun_NotAnOrphanRunningLease()
    {
        await using var test = await Harness.Start();
        test.Dispatcher.Fail = true;
        using var request = Request("operate", HttpMethod.Post, "/api/data-migration/SS/jobs/MyTeam/runs");
        request.Content = JsonContent.Create(new MigrationTriggerRequest());
        using var response = await test.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var run = await test.Db.DataMigrationRuns.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Failed", run.Status);
        Assert.Equal(1, run.FailedCount);
        Assert.DoesNotContain(
            "private",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(
            "DispatchFailed",
            (await test.Db.DataMigrationFailures.SingleAsync(TestContext.Current.CancellationToken)).ErrorType
        );
    }

    [Fact]
    public async Task DisabledTrigger_DoesNotAcquireRunOrDispatch()
    {
        await using var test = await Harness.Start();
        test.Dispatcher.Disabled = true;
        using var request = Request("operate", HttpMethod.Post, "/api/data-migration/SS/jobs/MyTeam/runs");
        request.Content = JsonContent.Create(new MigrationTriggerRequest());
        using var response = await test.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(await test.Db.DataMigrationRuns.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, test.Coordinator.Starts);
        Assert.Null(test.Dispatcher.Enqueued);
    }

    [Fact]
    public async Task Controls_AreSourceAndJobScoped_Audited_AndCannotEnableConfiguration()
    {
        await using var test = await Harness.Start();
        foreach (var route in new[] { "jobs/Training/control/abort", "control/pause", "jobs/Training/control/resume" })
        {
            using var request = Request("operate", HttpMethod.Post, "/api/data-migration/SS/" + route);
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var response = await test.Client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        var all = await test.Db.DataMigrationControls.ToListAsync(TestContext.Current.CancellationToken);
        Assert.True(all.Single(x => x.Source == "SS").IsPaused);
        Assert.False(all.Single(x => x.Source == "SS:Training").IsPaused);
        Assert.Null(all.Single(x => x.Source == "SS:Training").AbortRequestedOn);
        Assert.DoesNotContain(all, x => x.Source.StartsWith("CASS") || x.Source == "SS:Scheduling");
        Assert.All(all, x => Assert.Equal(Harness.Actor.ToString(), x.ChangedBy));
        Assert.Equal(3, await test.Db.AuditRecords.CountAsync(TestContext.Current.CancellationToken));
        var status = await test
            .App.Services.GetRequiredService<DataMigrationOperations>()
            .StatusAsync("SS", "Training", TestContext.Current.CancellationToken);
        Assert.False(status.Enabled); // Resume never mutates disabled options.
        Assert.True(status.SourceControl.Paused);
    }

    [Theory]
    [InlineData("application/x-www-form-urlencoded", "a=1")]
    [InlineData("multipart/form-data; boundary=x", "--x--")]
    [InlineData("text/plain", "{}")]
    [InlineData(null, null)]
    public async Task UnsafePost_RequiresJsonContentType_BeforeAuditOrControl(string? contentType, string? body)
    {
        await using var test = await Harness.Start();
        using (var request = Request("operate", HttpMethod.Post, "/api/data-migration/SS/control/pause"))
        {
            if (contentType is not null)
            {
                request.Content = new StringContent(body!, Encoding.UTF8);
                request.Content.Headers.Remove("Content-Type");
                request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            }
            using var rejected = await test.Client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, rejected.StatusCode);
        }
        Assert.Empty(await test.Db.DataMigrationControls.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await test.Db.AuditRecords.ToListAsync(TestContext.Current.CancellationToken));

        using var accepted = Request("operate", HttpMethod.Post, "/api/data-migration/SS/control/pause");
        accepted.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var response = await test.Client.SendAsync(accepted, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await test.Db.DataMigrationControls.SingleAsync(TestContext.Current.CancellationToken)).IsPaused);
        Assert.Single(await test.Db.AuditRecords.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("operate")]
    public async Task FlagOff_ConditionalApplicationPart_RemovesRoutes_Returning404Not500(string mode)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["FeatureFlags:DataMigration:Enabled"] = "false" }
        );
        builder.Logging.ClearProviders();
        builder.Services.AddDataMigrationModule(builder.Configuration);
        builder.Services.AddAuthorizationModule();
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, Handler>("Test", _ => { });
        builder.Services.RemoveAll<IClaimsTransformation>();
        builder.Services.AddSingleton<IClaimsTransformation, NoTransform>();
        var mvc = builder
            .Services.AddControllers()
            .ConfigureApplicationPartManager(parts =>
            {
                // Model the bug state: the module assembly is discovered as an MVC part.
                parts.ApplicationParts.Clear();
                parts.ApplicationParts.Add(new AssemblyPart(typeof(DataMigrationController).Assembly));
            });
        // Program.cs wiring.
        mvc.AddConditionalApplicationPart<DataMigrationController>(
            DataMigrationModule.IsModuleEnabled(builder.Configuration)
        );
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        Assert.False(DataMigrationModule.IsModuleEnabled(builder.Configuration));
        using var request = Request(mode, HttpMethod.Get, "/api/data-migration/SS/status");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("markdown")]
    public async Task CompleteExports_AreScopedRedactedAuditedAndNotPageLimited(string format)
    {
        await using var test = await Harness.Start();
        var id = await test.Seed(257);
        using var request = Request(
            "operate",
            HttpMethod.Get,
            $"/api/data-migration/SS/jobs/MyTeam/runs/{id}/export/{format}"
        );
        using var response = await test.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("SnapshotComplete", body);
        Assert.Contains("FutureGapRequiresSourceRepair", body);
        Assert.DoesNotContain("private", body);
        Assert.DoesNotContain("password", body);
        Assert.DoesNotContain("Photo", body);
        Assert.Contains("sha256:", body);
        // All 257 outcomes plus one failure, not the default50 or maximum200 UI page.
        Assert.Equal(257, body.Split('\n').Count(x => x.StartsWith(format == "csv" ? "\"Outcome\"" : "| Outcome |")));
        Assert.Single(body.Split('\n').Where(x => x.StartsWith(format == "csv" ? "\"Failure\"" : "| Failure |")));
        Assert.Contains("258", body.Split('\n').Single(x => x.Contains("SnapshotComplete")));
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Contains(id.ToString(), response.Content.Headers.ContentDisposition!.FileName);
        Assert.Single(await test.Db.AuditRecords.ToListAsync(TestContext.Current.CancellationToken));
        using var findings = Request(
            "operate",
            HttpMethod.Get,
            $"/api/data-migration/SS/jobs/MyTeam/runs/{id}/findings/failures"
        );
        using var failureResponse = await test.Client.SendAsync(findings, TestContext.Current.CancellationToken);
        var json = await failureResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("errorMessage", json);
        Assert.DoesNotContain("private", json);
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("markdown")]
    public void CellEncoding_PreventsFormulaHtmlAndTableInjection(string format)
    {
        var text =
            format == "csv"
                ? DataMigrationOperations.Csv("=HYPERLINK(\"private\")")
                : DataMigrationOperations.Markdown("<script>|`private`\r\n");
        if (format == "csv")
            Assert.StartsWith("\"'=", text);
        else
        {
            Assert.DoesNotContain("<script>", text);
            Assert.Contains("\\|", text);
            Assert.DoesNotContain("\n", text);
        }
    }

    [Theory]
    [InlineData(true, false, "NeedsAction")]
    [InlineData(false, false, "NeedsAction")]
    [InlineData(false, true, "Pass")]
    public async Task ReportVerdict_RequiresAppliedCompletedAndActualIndependentComparison(
        bool dryRun,
        bool oracle,
        string expected
    )
    {
        await using var test = await Harness.Start();
        var run = new DataMigrationRun
        {
            Source = "SS",
            Job = "Report",
            Status = "Completed",
            DryRun = dryRun,
        };
        test.Db.DataMigrationRuns.Add(run);
        if (oracle)
            test.Db.DataMigrationOutcomes.Add(
                new()
                {
                    RunId = run.Id,
                    EntityType = "SourceOracleValidation",
                    LegacyId = run.Id.ToString(),
                    Outcome = "Migrated",
                    Reason = "ActualIndependentOracleRowsMatched",
                }
            );
        await test.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var value = await test
            .App.Services.GetRequiredService<DataMigrationOperations>()
            .RunAsync("SS", "Report", run.Id, TestContext.Current.CancellationToken);
        Assert.Equal(expected, value.Verdict);
        // A stored failed finding must never be hidden by a stale Completed run label.
        test.Db.DataMigrationOutcomes.Add(
            new()
            {
                RunId = run.Id,
                EntityType = "SourceOracleValidation",
                LegacyId = Guid.NewGuid().ToString(),
                Outcome = "Failed",
                Reason = "ActualIndependentOracleDifferencesOrUnsupportedSurfaces",
            }
        );
        await test.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        value = await test
            .App.Services.GetRequiredService<DataMigrationOperations>()
            .RunAsync("SS", "Report", run.Id, TestContext.Current.CancellationToken);
        Assert.Equal("Fail", value.Verdict);
    }

    [Theory]
    [InlineData("/findings/outcomes?offset=-1", HttpStatusCode.BadRequest)]
    [InlineData("/findings/outcomes?limit=201", HttpStatusCode.BadRequest)]
    [InlineData("/findings/raw", HttpStatusCode.BadRequest)]
    [InlineData("/export/raw", HttpStatusCode.BadRequest)]
    public async Task Findings_RejectInvalidPageKindAndFormat(string suffix, HttpStatusCode expected)
    {
        await using var test = await Harness.Start();
        var id = await test.Seed();
        using var request = Request("operate", HttpMethod.Get, $"/api/data-migration/SS/jobs/MyTeam/runs/{id}{suffix}");
        using var response = await test.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(expected, response.StatusCode);
    }

    private static HttpRequestMessage Request(string mode, HttpMethod method, string uri)
    {
        var value = new HttpRequestMessage(method, uri);
        value.Headers.Add("X-Test-Mode", mode);
        return value;
    }

    private sealed class Harness(
        WebApplication app,
        SqliteConnection connection,
        UnifiedDbContext db,
        Coordinator coordinator,
        Dispatcher dispatcher
    ) : IAsyncDisposable
    {
        public static readonly Guid Actor = Guid.Parse("a3000000-0000-0000-0000-000000000001");
        public WebApplication App => app;
        public HttpClient Client { get; } = app.GetTestClient();
        public UnifiedDbContext Db => db;
        public Coordinator Coordinator => coordinator;
        public Dispatcher Dispatcher => dispatcher;

        public static async Task<Harness> Start()
        {
            var connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var db = new SqliteTestUnifiedDbContext(
                new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(connection).Options
            );
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DataMigration:Sources:SS:CutoverDate"] = "2026-12-01",
                    ["DataMigration:Sources:SS:Enabled"] = "false",
                    // Module must be enabled so its policies and operations register (SS source stays disabled).
                    ["FeatureFlags:DataMigration:Enabled"] = "true",
                }
            );
            builder.Logging.ClearProviders();
            builder.Services.AddDataMigrationModule(builder.Configuration);
            builder.Services.AddAuthorizationModule();
            builder
                .Services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, Handler>("Test", _ => { });
            builder.Services.RemoveAll<IClaimsTransformation>();
            builder.Services.AddSingleton<IClaimsTransformation, NoTransform>();
            builder.Services.AddSingleton<UnifiedDbContext>(db);
            var coordinator = new Coordinator(db);
            var dispatcher = new Dispatcher();
            builder.Services.RemoveAll<IMigrationRunCoordinator>();
            builder.Services.RemoveAll<IMigrationJobDispatcher>();
            builder.Services.AddSingleton<IMigrationRunCoordinator>(coordinator);
            builder.Services.AddSingleton<IMigrationJobDispatcher>(dispatcher);
            builder
                .Services.AddControllers()
                .ConfigureApplicationPartManager(parts =>
                {
                    parts.ApplicationParts.Clear();
                    parts.ApplicationParts.Add(new AssemblyPart(typeof(DataMigrationController).Assembly));
                });
            var app = builder.Build();
            app.Use(
                async (context, next) =>
                {
                    try
                    {
                        await next();
                    }
                    catch (global::Unified.Infrastructure.ErrorHandling.ForbiddenException)
                    {
                        context.Response.StatusCode = 403;
                    }
                }
            );
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync(TestContext.Current.CancellationToken);
            return new(app, connection, db, coordinator, dispatcher);
        }

        public async Task<Guid> Seed(int rows = 1)
        {
            var run = new DataMigrationRun
            {
                Source = "SS",
                Job = "MyTeam",
                Status = "Failed",
                DryRun = true,
            };
            db.DataMigrationRuns.Add(run);
            var other = new DataMigrationRun
            {
                Source = "SS",
                Job = "Training",
                Status = "Failed",
                DryRun = true,
            };
            db.DataMigrationRuns.Add(other);
            for (var n = 0; n < rows; n++)
                db.DataMigrationOutcomes.Add(
                    new()
                    {
                        RunId = run.Id,
                        EntityType = "Shift",
                        LegacyId = n == 0 ? "private source name" : n.ToString(),
                        Outcome = "NeedsAction",
                        Reason = n == 0 ? "private password=credential" : "FutureGapRequiresSourceRepair",
                    }
                );
            db.DataMigrationOutcomes.Add(
                new()
                {
                    RunId = other.Id,
                    EntityType = "User",
                    LegacyId = "1",
                    Outcome = "Failed",
                    Reason = "private other job",
                }
            );
            db.DataMigrationFailures.Add(
                new()
                {
                    RunId = run.Id,
                    Source = "SS",
                    EntityType = "User",
                    LegacyId = "private user",
                    ErrorType = "ExecutionFailed",
                    ErrorMessage = "private password=credential Photo",
                    Watermark = "private",
                    OccurredOn = DateTimeOffset.UtcNow,
                }
            );
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return run.Id;
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class NoTransform : IClaimsTransformation
    {
        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal) => Task.FromResult(principal);
    }

    private sealed class Handler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder
    ) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var mode = Request.Headers["X-Test-Mode"].ToString();
            if (mode == "anonymous")
                return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim>();
            if (mode != "unresolved")
                claims.Add(new(UnifiedClaimTypes.UserId, Harness.Actor.ToString()));
            if (mode is "operate" or "both" or "unresolved")
                claims.Add(new(UnifiedClaimTypes.Permission, MigrationPermissions.Operate));
            if (mode is "both" or "rollback-only")
                claims.Add(new(UnifiedClaimTypes.Permission, MigrationPermissions.Rollback));
            return Task.FromResult(
                AuthenticateResult.Success(
                    new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")), "Test")
                )
            );
        }
    }

    private sealed class Dispatcher : IMigrationJobDispatcher
    {
        public Guid? Enqueued { get; private set; }
        public bool Fail { get; set; }
        public bool Disabled { get; set; }

        public void Validate(string job, bool dryRun)
        {
            if (Disabled)
                throw new InvalidOperationException("private disabled config");
        }

        public void Enqueue(Guid runId)
        {
            if (Fail)
                throw new InvalidOperationException("private credential");
            Enqueued = runId;
        }
    }

    private sealed class Coordinator(UnifiedDbContext db) : IMigrationRunCoordinator
    {
        public int Starts { get; private set; }

        // Adapter tests isolate HTTP dispatch from the separately exercised real PG coordinator.
        public async Task<DataMigrationRun?> TryStartJobAsync(
            string job,
            bool dryRun,
            DateOnly cutover,
            CancellationToken cancellationToken,
            DateOnly? historyStartDate = null
        )
        {
            Starts++;
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
            await db.SaveChangesAsync(cancellationToken);
            return run;
        }

        public Task<bool> RecoverStaleRunAsync(Guid runId, DateTimeOffset staleBefore, CancellationToken ct) =>
            Task.FromResult(false);
    }
}
