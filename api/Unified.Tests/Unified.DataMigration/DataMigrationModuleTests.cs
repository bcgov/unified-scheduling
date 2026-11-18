using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Unified.DataMigration;
using Unified.DataMigration.FeatureFlags;
using Unified.DataMigration.Options;
using Unified.DataMigration.Services;

namespace Unified.Tests.DataMigration;

public sealed class DataMigrationModuleTests
{
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
    public async Task AddDataMigrationModule_WhenEnabledWithValidSource_PreservesRegistrationAndBinding()
    {
        var builder = CreateBuilder(true);
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["DataMigration:Sources:SS:ConnectionString"] = "Host=source",
                ["DataMigration:Sources:SS:CronSchedule"] = "0 * * * *",
                ["DataMigration:Sources:SS:BatchSize"] = "10",
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
