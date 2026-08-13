using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Api.Controllers;
using Unified.Api.Models;
using Unified.Api.Options;
using Unified.Calendar.FeatureFlags;
using Unified.Common.FeatureFlags;
using Unified.UserManagement.FeatureFlags;

namespace Unified.Tests.Api.Controllers;

public class ConfigControllerTests
{
    [Fact]
    public void Get_Should_Return_FeatureFlags_And_ApplicationSettings()
    {
        var featureFlags = new IFeatureFlags[]
        {
            new CalendarFeatureFlags { Enabled = true },
            new UserManagementFeatureFlags
            {
                Enabled = true,
                UserBadgeNumber = new UserBadgeNumberFlags { Enabled = true },
            },
        };
        var applicationOptions = Options.Create(
            new ApplicationOptions { Name = "Unified Scheduling", SupportEmail = "support@example.com" }
        );
        var controller = new ConfigController(NullLogger<ConfigController>.Instance, featureFlags, applicationOptions);

        var result = controller.Get();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ConfigResponse>(okResult.Value);
        Assert.NotNull(response.FeatureFlags.Calendar);
        Assert.NotNull(response.FeatureFlags.UserManagement);
        Assert.Null(response.FeatureFlags.Stats);
        Assert.Null(response.FeatureFlags.Training);
        Assert.Equal("Unified Scheduling", response.ApplicationName);
        Assert.Equal("support@example.com", response.SupportEmail);
    }
}
