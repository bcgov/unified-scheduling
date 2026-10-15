using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Unified.Core.Pdf;
using Unified.Infrastructure.Pdf;

namespace Unified.Tests.Infrastructure.Pdf;

public sealed class ScheduleDistributionRenderingOptionsValidatorTests
{
    private readonly ScheduleDistributionRenderingOptionsValidator _validator =
        new(new TestHostEnvironment { EnvironmentName = Environments.Development });

    [Fact]
    public void Validate_WhenFrontendBaseUrlIsMissing_FailsWithoutThrowing()
    {
        var result = _validator.Validate(null, new ScheduleDistributionRenderingOptions());

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains(nameof(ScheduleDistributionRenderingOptions.FrontendBaseUrl)));
    }

    [Fact]
    public void Validate_WhenFrontendBaseUrlIsPlaceholder_FailsWithoutThrowing()
    {
        var result = _validator.Validate(
            null,
            new ScheduleDistributionRenderingOptions { FrontendBaseUrl = "__SCHEDULE_DISTRIBUTION_FRONTEND_URL__" }
        );

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Validate_WhenConcurrencyIsOutsideAllowedRange_Fails(int concurrency)
    {
        var result = _validator.Validate(
            null,
            new ScheduleDistributionRenderingOptions
            {
                FrontendBaseUrl = "http://localhost:5173",
                MaximumConcurrentRenders = concurrency,
            }
        );

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains(nameof(ScheduleDistributionRenderingOptions.MaximumConcurrentRenders)));
    }

    [Theory]
    [InlineData("http://localhost:5173/calendar", "Development", true)]
    [InlineData("https://example.test/app/", "Production", true)]
    [InlineData("http://example.test", "Production", false)]
    [InlineData("ftp://example.test", "Development", false)]
    [InlineData("https://user@example.test", "Production", false)]
    [InlineData("https://example.test?query=1", "Production", false)]
    [InlineData("https://example.test#fragment", "Production", false)]
    public void Validate_FrontendUrl_EnforcesTrustedEnvironmentAppropriateUrl(
        string frontendBaseUrl,
        string environmentName,
        bool expectedSuccess
    )
    {
        var validator = new ScheduleDistributionRenderingOptionsValidator(
            new TestHostEnvironment { EnvironmentName = environmentName }
        );

        var result = validator.Validate(
            null,
            new ScheduleDistributionRenderingOptions { FrontendBaseUrl = frontendBaseUrl }
        );

        Assert.Equal(expectedSuccess, result.Succeeded);
    }

    [Fact]
    public async Task InvalidConfiguration_DoesNotPreventServiceProviderCreation_ButFailsRenderingAsUnavailable()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddSingleton<IHostEnvironment>(
            new TestHostEnvironment { EnvironmentName = Environments.Development }
        );
        services.Configure<global::Unified.Infrastructure.Options.KeycloakOptions>(_ => { });
        services.AddPlaywrightPdfRendering(new ConfigurationBuilder().Build());

        await using var serviceProvider = services.BuildServiceProvider();
        var renderer = serviceProvider.GetRequiredService<ISchedulePdfRenderer>();

        var exception = await Assert.ThrowsAsync<PdfRenderingException>(() =>
            renderer.RenderAsync(
                new SchedulePdfRenderRequest
                {
                    View = SchedulePdfView.Schedule,
                    LocationId = 7,
                    StartDate = new DateOnly(2026, 8, 24),
                    EndDate = new DateOnly(2026, 8, 25),
                    Period = SchedulePdfPeriod.Day,
                },
                TestContext.Current.CancellationToken
            )
        );

        Assert.True(exception.IsServiceUnavailable);
        Assert.IsType<OptionsValidationException>(exception.InnerException);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = string.Empty;
        public string ApplicationName { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
