using Hangfire;
using Hangfire.Common;
using Hangfire.InMemory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Unified.Common.FeatureFlags;
using Unified.DataMigration;
using Unified.DataMigration.FeatureFlags;
using Unified.DataMigration.Options;
using Unified.DataMigration.Services;

namespace Unified.Tests.DataMigration;

public sealed class DataMigrationModuleTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void AddDataMigrationModule_ExposesFeatureFlagForWebConfig(bool? enabled)
    {
        var builder = CreateBuilder(enabled);
        using var provider = builder.Services.BuildServiceProvider();

        var flag = Assert.Single(provider.GetServices<IFeatureFlags>());

        Assert.Equal("DataMigration", flag.Source);
        Assert.Equal(enabled == true, Assert.IsType<DataMigrationFeatureFlags>(flag).Enabled);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(null, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task Startup_CleansOnlyRetiredSsSchedules(bool? enabled, bool hangfireAvailable)
    {
        var builder = CreateBuilder(enabled);
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["DataMigration:Sources:SS:ConnectionString"] = "Host=source",
                ["DataMigration:Sources:SS:BatchSize"] = "10",
                ["DataMigration:Sources:SS:CutoverDate"] = "2026-12-01",
            }
        );
        using var storage = new InMemoryStorage();
        var manager = new RecurringJobManager(storage);
        string[] current = ["myteam", "training", "scheduling", "report"];
        string[] ids = ["data-migration:ss", "data-migration:cass", .. current.Select(x => $"data-migration:ss:{x}")];
        foreach (var id in ids)
            manager.AddOrUpdate(id, Job.FromExpression(() => ScheduleStub.Execute()), "0 * * * *", new());
        if (hangfireAvailable)
            builder.Services.AddSingleton<IRecurringJobManager>(manager);
        using var host = builder.Build();

        await host.StartAsync(TestContext.Current.CancellationToken);

        using var connection = storage.GetConnection();
        var remaining = connection.GetAllItemsFromSet("recurring-jobs");
        Assert.Contains("data-migration:cass", remaining);
        Assert.Equal(!hangfireAvailable, remaining.Contains("data-migration:ss"));
        foreach (var job in current)
            Assert.Equal(!hangfireAvailable || enabled == true, remaining.Contains($"data-migration:ss:{job}"));
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    public sealed class ScheduleStub
    {
        public static void Execute() { }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task AddDataMigrationModule_WhenDisabled_DoesNotRegisterOrValidateSources(bool? enabled)
    {
        var builder = CreateBuilder(enabled);
        using var host = builder.Build();

        await host.StartAsync(TestContext.Current.CancellationToken);

        Assert.False(host.Services.GetRequiredService<IOptions<DataMigrationFeatureFlags>>().Value.Enabled);
        Assert.DoesNotContain(
            builder.Services,
            descriptor =>
                descriptor.ServiceType == typeof(DataMigrationOrchestrator)
                || descriptor.ServiceType == typeof(DataMigrationControlService)
                || descriptor.ServiceType == typeof(IConfigureOptions<DataMigrationOptions>)
                || descriptor.ServiceType == typeof(IValidateOptions<DataMigrationOptions>)
        );
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AddDataMigrationModule_WhenEnabled_ValidatesSourcesAtStartup()
    {
        var builder = CreateBuilder(true);
        using var host = builder.Build();

        var error = await Assert.ThrowsAsync<OptionsValidationException>(() =>
            host.StartAsync(TestContext.Current.CancellationToken)
        );

        Assert.Contains(error.Failures, failure => failure.Contains("SS:ConnectionString", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AddDataMigrationModule_WhenEnabledWithoutCutover_RejectsOtherwiseValidSource()
    {
        var builder = CreateBuilder(true);
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["DataMigration:Sources:SS:ConnectionString"] = "Host=source",
                ["DataMigration:Sources:SS:BatchSize"] = "10",
            }
        );
        using var host = builder.Build();

        var error = await Assert.ThrowsAsync<OptionsValidationException>(() =>
            host.StartAsync(TestContext.Current.CancellationToken)
        );

        Assert.Contains(error.Failures, failure => failure.Contains("SS:CutoverDate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AddDataMigrationModule_WhenEnabledWithValidSource_PreservesRegistrationAndBinding()
    {
        var builder = CreateBuilder(true);
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["DataMigration:Sources:SS:ConnectionString"] = "Host=source",
                ["DataMigration:Sources:SS:CronSchedule"] = "0 * * * *",
                ["DataMigration:Sources:SS:BatchSize"] = "10",
                ["DataMigration:Sources:SS:CutoverDate"] = "2026-12-01",
            }
        );
        using var host = builder.Build();

        await host.StartAsync(TestContext.Current.CancellationToken);

        var source = host.Services.GetRequiredService<IOptions<DataMigrationOptions>>().Value.Sources.SS;
        Assert.True(source.Enabled);
        Assert.Equal("Host=source", source.ConnectionString);
        Assert.Equal(10, source.BatchSize);
        Assert.Contains(
            builder.Services,
            descriptor =>
                descriptor.ServiceType == typeof(DataMigrationOrchestrator)
                && descriptor.Lifetime == ServiceLifetime.Scoped
        );
        Assert.Contains(
            builder.Services,
            descriptor =>
                descriptor.ServiceType == typeof(DataMigrationControlService)
                && descriptor.Lifetime == ServiceLifetime.Scoped
        );
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static HostApplicationBuilder CreateBuilder(bool? enabled)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        var settings = new Dictionary<string, string?>
        {
            ["DataMigration:Sources:SS:Enabled"] = "true",
            ["DataMigration:Sources:SS:BatchSize"] = "0",
        };
        if (enabled.HasValue)
        {
            settings["FeatureFlags:DataMigration:Enabled"] = enabled.Value.ToString();
        }

        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddDataMigrationModule(builder.Configuration);
        return builder;
    }
}
