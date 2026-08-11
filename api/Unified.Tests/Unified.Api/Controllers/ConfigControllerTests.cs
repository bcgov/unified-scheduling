using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Api.Controllers;
using Unified.Api.Models;
using Unified.Api.Options;
using Unified.Calendar.FeatureFlags;
using Unified.Common.FeatureFlags;
using Unified.Scheduling.FeatureFlags;
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
            new SchedulingFeatureFlags { Enabled = true },
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
        Assert.True(response.FeatureFlags.ContainsKey(CalendarFeatureFlags.SourceName));
        Assert.True(response.FeatureFlags.ContainsKey(UserManagementFeatureFlags.SourceName));
        Assert.False(response.FeatureFlags.ContainsKey("Stats"));
        Assert.False(response.FeatureFlags.ContainsKey("Training"));

        Assert.IsType<CalendarFeatureFlags>(response.FeatureFlags[CalendarFeatureFlags.SourceName]);
        Assert.IsType<UserManagementFeatureFlags>(response.FeatureFlags[UserManagementFeatureFlags.SourceName]);
        Assert.Equal("Unified Scheduling", response.ApplicationName);
        Assert.Equal("support@example.com", response.SupportEmail);
    }
}
