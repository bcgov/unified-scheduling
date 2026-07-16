using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Api.Controllers;
using Unified.Api.Models;
using Unified.Api.Options;
using Unified.FeatureFlags;

namespace Unified.Tests.Api.Controllers;

public class ConfigControllerTests
{
    private sealed class TestFeatureFlags(FeatureFlags.FeatureFlags current) : IFeatureFlags
    {
        public FeatureFlags.FeatureFlags Current { get; } = current;
    }

    [Fact]
    public void Get_Should_Return_FeatureFlags_And_ApplicationSettings()
    {
        var featureFlags = new FeatureFlags.FeatureFlags { CalendarModule = true, MyTeamsModule = true };
        var applicationOptions = Options.Create(
            new ApplicationOptions { Name = "Unified Scheduling", SupportEmail = "support@example.com" }
        );
        var controller = new ConfigController(
            NullLogger<ConfigController>.Instance,
            new TestFeatureFlags(featureFlags),
            applicationOptions
        );

        var result = controller.Get();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ConfigResponse>(okResult.Value);
        Assert.True(response.FeatureFlags.CalendarModule);
        Assert.True(response.FeatureFlags.MyTeamsModule);
        Assert.Equal("Unified Scheduling", response.ApplicationName);
        Assert.Equal("support@example.com", response.SupportEmail);
    }
}
