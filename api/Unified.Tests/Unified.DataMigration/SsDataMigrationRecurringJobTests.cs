using Hangfire;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.Common.Jobs;
using Unified.DataMigration;
using Unified.DataMigration.FeatureFlags;
using Unified.DataMigration.Jobs;
using Unified.DataMigration.Options;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.DataMigration;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.DataMigration;

// The old blocked-shell assertions are deliberately replaced, not retained through a
// compatibility constructor. PostgreSQL execution is exercised separately in the rehearsal.
public sealed class SsDataMigrationRecurringJobTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:;Foreign Keys=True");
    private readonly UnifiedDbContext _db;
    private readonly SaveCounter _saves = new();

    public SsDataMigrationRecurringJobTests()
    {
        _connection.Open();
        _db = new SqliteTestUnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(_connection).AddInterceptors(_saves).Options
        );
        _db.Database.EnsureCreated();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Identity_UsesPerJobGatesWithoutSerializingIndependentJobs(bool featureEnabled, bool sourceEnabled)
    {
        var job = CreateJob(featureEnabled, sourceEnabled);
        Assert.Equal("data-migration:ss:myteam", job.JobName);
        Assert.Equal(featureEnabled && sourceEnabled ? "*/15 * * * *" : "", job.CronSchedule);
        var method = typeof(SsDataMigrationRecurringJob).GetMethod(nameof(SsDataMigrationRecurringJob.Execute))!;
        Assert.Equal(
            0,
            Assert
                .Single(
                    method.GetCustomAttributes(typeof(AutomaticRetryAttribute), false).Cast<AutomaticRetryAttribute>()
                )
                .Attempts
        );
        Assert.Empty(method.GetCustomAttributes(typeof(DisableConcurrentExecutionAttribute), false));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Execute_DisabledFeatureOrSource_DoesNotTouchMigrationState(
        bool featureEnabled,
        bool sourceEnabled
    )
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateJob(featureEnabled, sourceEnabled).Execute(null, TestContext.Current.CancellationToken)
        );
        Assert.Equal("SS migration job is disabled.", error.Message);
        Assert.Equal(0, _saves.Count);
        Assert.Empty(_db.DataMigrationRuns);
        Assert.Empty(_db.DataMigrationFailures);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execute_MissingJcConsent_LeavesCassAndSsStateUntouched(bool dryRun)
    {
        var cass = new DataMigrationRun
        {
            Source = "CASS",
            Status = "Running",
            DryRun = !dryRun,
        };
        _db.DataMigrationRuns.Add(cass);
        _db.DataMigrationWatermarks.Add(
            new DataMigrationWatermark
            {
                Source = "CASS",
                EntityType = "Region",
                LegacyId = "42",
                SourceChangedAtUtc = DateTimeOffset.UtcNow,
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var before = _saves.Count;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateJob(dryRun: dryRun).Execute(null, TestContext.Current.CancellationToken)
        );
        Assert.Equal("SS migration requires explicit false JC expiry flags.", error.Message);
        Assert.Equal(before, _saves.Count);
        Assert.Equal("Running", Assert.Single(_db.DataMigrationRuns).Status);
        Assert.Equal(cass.Id, Assert.Single(_db.DataMigrationRuns).Id);
        Assert.Equal("42", Assert.Single(_db.DataMigrationWatermarks).LegacyId);
        Assert.Empty(_db.DataMigrationFailures);
        Assert.Empty(_db.DataMigrationRecords);
    }

    [Fact]
    public async Task Execute_PreflightFailure_DoesNotAlterExistingSsRun()
    {
        var active = new DataMigrationRun
        {
            Source = "SS",
            Job = "MyTeam",
            Status = "Running",
        };
        _db.DataMigrationRuns.Add(active);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateJob().Execute(null, TestContext.Current.CancellationToken)
        );
        Assert.Equal(active.Id, Assert.Single(_db.DataMigrationRuns).Id);
        Assert.Equal("Running", Assert.Single(_db.DataMigrationRuns).Status);
        Assert.Empty(_db.DataMigrationFailures);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("true")]
    [InlineData("not-a-bool Password=secret")]
    public async Task Execute_InvalidJcConsent_FailsBeforeStorageWithoutEchoingInput(string? consent)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["JCInterface:ExpireRegions"] = consent,
                    ["JCInterface:ExpireLocations"] = "false",
                    ["JCInterface:ExpireCourtRooms"] = "false",
                }
            )
            .Build();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateJob(configuration: config).Execute(null, TestContext.Current.CancellationToken)
        );
        Assert.Equal("SS migration requires explicit false JC expiry flags.", error.Message);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("secret", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, _saves.Count);
        Assert.Empty(_db.DataMigrationRuns);
    }

    [Fact]
    public async Task Execute_CancelledBeforeGuard_DoesNotCreateRun()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreateJob().Execute(null, cancellation.Token)
        );
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(0, _saves.Count);
        Assert.Empty(_db.DataMigrationRuns);
    }

    [Fact]
    public async Task Execute_DisabledJob_DoesNotSave()
    {
        var options = CreateOptions();
        options.Sources.SS.Jobs.MyTeam.Enabled = false;
        var job = CreateJob(options: options);
        Assert.Empty(job.CronSchedule);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            job.Execute(null, TestContext.Current.CancellationToken)
        );
        Assert.Equal("SS migration job is disabled.", error.Message);
        Assert.Equal(0, _saves.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execute_PreflightCancellationOrConfigurationFailure_IsSanitized(bool callerCancelled)
    {
        using var cancellation = new CancellationTokenSource();
        if (callerCancelled)
            cancellation.Cancel();
        if (callerCancelled)
        {
            var error = await Assert.ThrowsAsync<OperationCanceledException>(() =>
                CreateJob().Execute(null, cancellation.Token)
            );
            Assert.Equal("SS migration was canceled.", error.Message);
            Assert.Null(error.InnerException);
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateJob().Execute(null, cancellation.Token)
            );
            Assert.Equal("SS migration requires explicit false JC expiry flags.", error.Message);
        }
        Assert.Equal(0, _saves.Count);
        Assert.Empty(_db.DataMigrationRuns);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-cron Password=secret")]
    public void Validate_DisabledSsSource_IgnoresMalformedSettings(string? schedule)
    {
        var options = CreateOptions();
        options.Sources.SS.Enabled = false;
        options.Sources.SS.CronSchedule = schedule;
        options.Sources.SS.ConnectionString = null;
        options.Sources.SS.BatchSize = 0;
        Assert.True(new DataMigrationOptionsValidator().Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("disable")]
    [InlineData("DISABLED")]
    [InlineData("0 * * * *")]
    [InlineData("*/15 9-17 * * MON-FRI")]
    public void Validate_LegacySourceCron_DoesNotOverridePerJobSchedule(string? schedule)
    {
        var options = CreateOptions();
        options.Sources.SS.CronSchedule = schedule;
        Assert.True(new DataMigrationOptionsValidator().Validate(null, options).Succeeded);
        Assert.Equal("*/15 * * * *", CreateJob(options: options).CronSchedule);
    }

    [Theory]
    [InlineData("61 * * * *")]
    [InlineData("* * * *")]
    [InlineData("* * * * * *")]
    [InlineData("not-a-cron Password=secret")]
    public void Validate_SsSchedule_RejectsInvalidCronWithoutEchoingInput(string schedule)
    {
        var options = CreateOptions();
        options.Sources.SS.CronSchedule = schedule;
        var result = new DataMigrationOptionsValidator().Validate(null, options);
        Assert.True(result.Failed);
        Assert.Equal(
            "DataMigration:Sources:SS:CronSchedule must be a valid five-field cron expression.",
            Assert.Single(result.Failures)
        );
    }

    [Fact]
    public void Module_ResolvesFourScopedJobsAndDisabledSources()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddScoped<UnifiedDbContext>(_ => _db);
        services.AddDataMigrationModule(configuration);
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
        );
        using var scope = provider.CreateScope();
        var jobs = scope.ServiceProvider.GetServices<IRecurringJob>().ToList();
        Assert.Equal(
            new[]
            {
                "data-migration:ss:myteam",
                "data-migration:ss:report",
                "data-migration:ss:scheduling",
                "data-migration:ss:training",
            },
            jobs.Select(x => x.JobName).OrderBy(x => x)
        );
        Assert.All(jobs, job => Assert.Empty(job.CronSchedule));
        var source = scope.ServiceProvider.GetRequiredService<ISsLegacyMigrationSource>();
        Assert.IsType<SsLegacyMigrationSource>(source);
        Assert.Equal("SS", source.Source);
        Assert.Same(source, scope.ServiceProvider.GetRequiredService<ISsLegacyMigrationSource>());
        Assert.Null(scope.ServiceProvider.GetService<ICassLegacyMigrationSource>());
        Assert.False(scope.ServiceProvider.GetRequiredService<IOptions<DataMigrationFeatureFlags>>().Value.Enabled);
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DataMigrationOptions>>().Value;
        Assert.False(options.Sources.SS.Enabled);
        Assert.False(options.Sources.CASS.Enabled);
        Assert.True(options.Sources.SS.Jobs.MyTeam.DryRun);
    }

    private SsDataMigrationRecurringJob CreateJob(
        bool featureEnabled = true,
        bool sourceEnabled = true,
        bool dryRun = false,
        DataMigrationOptions? options = null,
        IConfiguration? configuration = null
    )
    {
        options ??= CreateOptions();
        options.Sources.SS.Enabled = sourceEnabled;
        options.Sources.SS.Jobs.MyTeam.DryRun = dryRun;
        var wrapped = Microsoft.Extensions.Options.Options.Create(options);
        var flags = Microsoft.Extensions.Options.Options.Create(
            new DataMigrationFeatureFlags { Enabled = featureEnabled }
        );
        return new SsDataMigrationRecurringJob(
            flags,
            wrapped,
            SsMigrationTestServices.Executor(_db, wrapped, flags, configuration)
        );
    }

    private static DataMigrationOptions CreateOptions() =>
        new()
        {
            Sources = new DataMigrationSourcesOptions
            {
                SS = new DataMigrationSourceOptions
                {
                    Enabled = true,
                    ConnectionString = "Host=ss-only;Password=secret",
                    CutoverDate = new DateOnly(2026, 12, 1),
                    Jobs = new SsJobOptions
                    {
                        MyTeam = new MigrationJobOptions { Enabled = true, CronSchedule = "*/15 * * * *" },
                    },
                },
            },
        };

    private sealed class SaveCounter : SaveChangesInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
