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
using Unified.DataMigration.Services;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.DataMigration;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.DataMigration;

public sealed class SsDataMigrationRecurringJobTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:;Foreign Keys=True");
    private readonly UnifiedDbContext _db;
    private readonly FakeSaveInterceptor _saveInterceptor = new();

    public SsDataMigrationRecurringJobTests()
    {
        _connection.Open();
        _db = new SqliteTestUnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(_saveInterceptor)
                .Options
        );
        _db.Database.EnsureCreated();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Identity_IsStableAndScheduleRemainsDisabledWithoutApprovedMappings(
        bool featureEnabled,
        bool sourceEnabled
    )
    {
        var job = CreateJob(featureEnabled, sourceEnabled);

        Assert.Equal("data-migration:ss", job.JobName);
        Assert.Equal(string.Empty, job.CronSchedule);
        var method = typeof(SsDataMigrationRecurringJob).GetMethod(nameof(SsDataMigrationRecurringJob.Execute))!;
        Assert.Equal(
            0,
            Assert
                .Single(
                    method.GetCustomAttributes(typeof(AutomaticRetryAttribute), false).Cast<AutomaticRetryAttribute>()
                )
                .Attempts
        );
        Assert.Single(method.GetCustomAttributes(typeof(DisableConcurrentExecutionAttribute), false));
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
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateJob(featureEnabled, sourceEnabled).Execute(null, TestContext.Current.CancellationToken)
        );

        Assert.Equal("SS migration is disabled.", exception.Message);
        Assert.Equal(0, _saveInterceptor.SaveCount);
        Assert.Empty(_db.DataMigrationRuns);
        Assert.Empty(_db.DataMigrationFailures);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execute_UnapprovedMappings_PersistsFailedSsRunAndLeavesCassUntouched(bool dryRun)
    {
        var cassRun = new DataMigrationRun
        {
            Source = "CASS",
            Status = "Running",
            DryRun = !dryRun,
        };
        var cassWatermark = new DataMigrationWatermark
        {
            Source = "CASS",
            EntityType = "Region",
            LegacyId = "42",
            SourceChangedAtUtc = DateTimeOffset.UtcNow,
        };
        _db.DataMigrationRuns.Add(cassRun);
        _db.DataMigrationWatermarks.Add(cassWatermark);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateJob(dryRun: dryRun).Execute(null, TestContext.Current.CancellationToken)
        );

        Assert.Equal("SS migration requires human approval of mapping contracts.", exception.Message);
        _db.ChangeTracker.Clear();
        var ssRun = Assert.Single(_db.DataMigrationRuns.Where(run => run.Source == "SS"));
        Assert.Equal("Failed", ssRun.Status);
        Assert.Equal(dryRun, ssRun.DryRun);
        Assert.NotNull(ssRun.CompletedOn);
        var failure = Assert.Single(_db.DataMigrationFailures);
        Assert.Equal(ssRun.Id, failure.RunId);
        Assert.Equal("SS", failure.Source);
        Assert.Equal("MappingApprovalRequired", failure.ErrorType);
        Assert.Null(exception.InnerException);
        Assert.Equal("Running", _db.DataMigrationRuns.Single(run => run.Id == cassRun.Id).Status);
        Assert.Equal("42", _db.DataMigrationWatermarks.Single().LegacyId);
        Assert.Empty(_db.DataMigrationRecords);
        Assert.DoesNotContain(_db.DataMigrationRuns, run => run.Status == "Completed");
    }

    [Fact]
    public async Task Execute_ExistingSsRun_RefusesDurableGuardWithoutFailureOrCompletion()
    {
        var active = new DataMigrationRun { Source = "SS", Status = "Running" };
        _db.DataMigrationRuns.Add(active);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateJob().Execute(null, TestContext.Current.CancellationToken)
        );

        Assert.Contains("durable run guard", exception.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.Equal(active.Id, Assert.Single(_db.DataMigrationRuns).Id);
        Assert.Equal("Running", _db.DataMigrationRuns.Single().Status);
        Assert.Empty(_db.DataMigrationFailures);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Execute_StorageError_DoesNotLeakExceptionOrReportCompleted(int failingSave)
    {
        _saveInterceptor.FailOnSave = failingSave;
        _saveInterceptor.Exception = new InvalidOperationException(
            "Password=secret; Host=private-host; source row PII"
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateJob().Execute(null, TestContext.Current.CancellationToken)
        );

        Assert.Equal("SS migration failed. Verify migration state storage.", exception.Message);
        Assert.Null(exception.InnerException);
        Assert.DoesNotContain("secret", exception.ToString(), StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.DoesNotContain(_db.DataMigrationRuns, run => run.Status == "Completed");
        Assert.All(
            _db.DataMigrationFailures,
            failure => Assert.DoesNotContain("secret", failure.ErrorMessage, StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task Execute_CancelledBeforeGuard_DoesNotCreateRun()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateJob().Execute(null, cancellation.Token));

        _db.ChangeTracker.Clear();
        Assert.Empty(_db.DataMigrationRuns);
    }

    [Fact]
    public async Task Execute_CancelledAfterGuard_StillPersistsMappingFailureAndFailedRun()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _saveInterceptor.CancelOnSecondSave = cancellation;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateJob().Execute(null, cancellation.Token)
        );

        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal("SS migration requires human approval of mapping contracts.", exception.Message);
        _db.ChangeTracker.Clear();
        Assert.Equal("Failed", Assert.Single(_db.DataMigrationRuns).Status);
        Assert.Equal("MappingApprovalRequired", Assert.Single(_db.DataMigrationFailures).ErrorType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execute_ProviderCancellation_DoesNotExposeProviderText(bool callerCancelled)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        if (callerCancelled)
            cancellation.Cancel();
        _saveInterceptor.FailOnSave = 1;
        _saveInterceptor.Exception = new OperationCanceledException(
            "Password=secret; source row PII",
            cancellation.Token
        );

        if (callerCancelled)
        {
            var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
                CreateJob().Execute(null, cancellation.Token)
            );
            Assert.Equal("SS migration was canceled.", exception.Message);
            Assert.Equal(cancellation.Token, exception.CancellationToken);
            Assert.Null(exception.InnerException);
        }
        else
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateJob().Execute(null, cancellation.Token)
            );
            Assert.Equal("SS migration failed. Verify migration state storage.", exception.Message);
            Assert.Null(exception.InnerException);
        }
        _db.ChangeTracker.Clear();
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
    public void Validate_SsSchedule_AcceptsDisabledOrParsedFiveFieldCron(string? schedule)
    {
        var options = CreateOptions();
        options.Sources.SS.CronSchedule = schedule;

        Assert.True(new DataMigrationOptionsValidator().Validate(null, options).Succeeded);
        Assert.Empty(CreateJob(options: options).CronSchedule);
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
    public void Module_ResolvesScopedSsJobAndSourceWithDisabledDefaults()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddScoped<UnifiedDbContext>(_ => _db);
        services.AddDataMigrationModule(configuration);
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
        );
        using var scope = provider.CreateScope();

        var job = Assert.IsType<SsDataMigrationRecurringJob>(
            Assert.Single(scope.ServiceProvider.GetServices<IRecurringJob>())
        );
        Assert.Equal("data-migration:ss", job.JobName);
        Assert.Empty(job.CronSchedule);
        var source = scope.ServiceProvider.GetRequiredService<ISsLegacyMigrationSource>();
        Assert.IsType<SsLegacyMigrationSource>(source);
        Assert.Equal("SS", source.Source);
        Assert.Same(source, scope.ServiceProvider.GetRequiredService<ISsLegacyMigrationSource>());
        Assert.Null(scope.ServiceProvider.GetService<ICassLegacyMigrationSource>());
        Assert.False(scope.ServiceProvider.GetRequiredService<IOptions<DataMigrationFeatureFlags>>().Value.Enabled);
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DataMigrationOptions>>().Value;
        Assert.False(options.Sources.SS.Enabled);
        Assert.False(options.Sources.CASS.Enabled);
    }

    private SsDataMigrationRecurringJob CreateJob(
        bool featureEnabled = true,
        bool sourceEnabled = true,
        bool dryRun = false,
        DataMigrationOptions? options = null
    )
    {
        options ??= CreateOptions();
        options.Sources.SS.Enabled = sourceEnabled;
        options.Sources.SS.DryRun = dryRun;
        return new SsDataMigrationRecurringJob(
            Microsoft.Extensions.Options.Options.Create(new DataMigrationFeatureFlags { Enabled = featureEnabled }),
            Microsoft.Extensions.Options.Options.Create(options),
            new DataMigrationControlService(_db),
            new DataMigrationOrchestrator(_db)
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
                    CronSchedule = "0 * * * *",
                },
                CASS = new DataMigrationSourceOptions
                {
                    Enabled = false,
                    ConnectionString = "Host=cass-only",
                    CronSchedule = "invalid",
                },
            },
        };

    private sealed class FakeSaveInterceptor : SaveChangesInterceptor
    {
        public int SaveCount { get; private set; }
        public int FailOnSave { get; set; }
        public Exception? Exception { get; set; }
        public CancellationTokenSource? CancelOnSecondSave { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            SaveCount++;
            if (SaveCount == 2)
                CancelOnSecondSave?.Cancel();
            if (SaveCount == FailOnSave)
            {
                throw Exception!;
            }
            return ValueTask.FromResult(result);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
