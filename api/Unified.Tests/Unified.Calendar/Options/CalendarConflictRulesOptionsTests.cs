using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Unified.Calendar;
using Unified.Calendar.Options;

namespace Unified.Tests.Calendar;

public sealed class CalendarConflictRulesOptionsTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Rules\":{}}")]
    [InlineData("{\"Rules\":{\"unknown\":[]}}")]
    [InlineData("{\"Rules\":{\"assignment\":[\"unknown\"]}}")]
    [InlineData("{\"Rules\":{\"assignment\":null}}")]
    public async Task Startup_WhenRulesMissingOrUnknown_FailsValidation(string conflictConfig)
    {
        using var host = CreateHost(conflictConfig);

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() =>
            host.StartAsync(TestContext.Current.CancellationToken)
        );

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains("CalendarConflicts:Rules", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task Startup_WhenArraysEmptyAndModulesDisabled_AllowsOnlyExplicitDirections()
    {
        using var host = CreateHost(
            """
            {"Rules":{"shift":[],"assignment":["leave"],"leave":[],"training":["holiday"],"holiday":[]}}
            """
        );

        await host.StartAsync(TestContext.Current.CancellationToken);
        var rules = host.Services.GetRequiredService<IOptions<CalendarConflictRulesOptions>>().Value;

        Assert.True(rules.ShouldReport("assignment", "leave"));
        Assert.True(rules.ShouldReport("training", "holiday"));
        Assert.False(rules.ShouldReport("leave", "assignment"));
        Assert.False(rules.ShouldReport("holiday", "training"));
        Assert.False(rules.ShouldReport("assignment", "assignment"));
        Assert.False(rules.ShouldReport("shift", "leave"));
        Assert.False(rules.ShouldReport("unknown", "leave"));
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static IHost CreateHost(string conflictConfig) =>
        new HostBuilder()
            .ConfigureAppConfiguration(configuration =>
                configuration.AddJsonStream(
                    new MemoryStream(
                        Encoding.UTF8.GetBytes(
                            """
                            {"FeatureFlags":{"Calendar":{"Enabled":true}},"CalendarDateTime":{"DefaultTimeZoneId":"UTC"},"CalendarConflicts":
                            """
                                + conflictConfig
                                + "}"
                        )
                    )
                )
            )
            .ConfigureServices((context, services) => services.AddCalendarModule(context.Configuration))
            .Build();
}
