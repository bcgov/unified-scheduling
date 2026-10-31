using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
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
        var brandingOptions = Options.Create(new BrandingOptions { SelectedBrand = "bcss" });
        var controller = new ConfigController(
            NullLogger<ConfigController>.Instance,
            featureFlags,
            applicationOptions,
            brandingOptions
        );

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
        Assert.Equal("bcss", response.SelectedBrand);
    }

    [Fact]
    public void Get_Should_Return_Default_SelectedBrand_When_Not_Configured()
    {
        var controller = new ConfigController(
            NullLogger<ConfigController>.Instance,
            [],
            Options.Create(new ApplicationOptions()),
            Options.Create(new BrandingOptions())
        );

        var result = controller.Get();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ConfigResponse>(okResult.Value);
        Assert.Equal("default", response.SelectedBrand);
        Assert.Empty(response.BrandingOptions);
    }

    [Fact]
    public void Get_Should_Bind_And_Expose_PerBrand_Settings_Without_Filling_Missing_Overrides()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Branding:SelectedBrand"] = "bcss",
                    ["Branding:brandingOptions:default:primaryColor"] = "#013366",
                    ["Branding:brandingOptions:default:dashboardMessage"] = "Choose a module.",
                    ["Branding:brandingOptions:default:header:title"] = "Default title",
                    ["Branding:brandingOptions:default:QuickLinks:0"] = "myProfile",
                    ["Branding:brandingOptions:bcss:backgroundColor"] = "#FEFEFE",
                    ["Branding:brandingOptions:bcss:navigationSelectedColor"] = "#e3a82b",
                    ["Branding:brandingOptions:bcss:secondaryLogoUrl"] = "/images/branding/quartech/logo.png",
                    ["Branding:brandingOptions:bcss:secondaryLogoAlt"] = "Quartech",
                    ["Branding:brandingOptions:bcss:backgroundImageUrl"] = "/images/branding/bcss/background.png",
                    ["Branding:brandingOptions:bcss:header:imageUrl"] = "/images/branding/bcss/BCSS_Dashboard_bg.png",
                    ["Branding:brandingOptions:bcss:header:logoUrl"] = "/images/branding/bcss/BCSS_Crest_cmyk_pos.png",
                    ["Branding:brandingOptions:bcss:header:logoAlt"] = "B.C. Sheriff Service Crest",
                    ["Branding:brandingOptions:bcss:header:poweredByText"] = "",
                }
            )
            .Build();
        var settings = configuration.GetSection("Branding").Get<BrandingOptions>()!;
        var controller = new ConfigController(
            NullLogger<ConfigController>.Instance,
            [],
            Options.Create(new ApplicationOptions()),
            Options.Create(settings)
        );
        var result = controller.Get();
        var response = Assert.IsType<ConfigResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("bcss", response.SelectedBrand);
        Assert.Equal("#013366", response.BrandingOptions["default"].PrimaryColor);
        Assert.Equal("Choose a module.", response.BrandingOptions["default"].DashboardMessage);
        Assert.Equal("Default title", response.BrandingOptions["default"].Header!.Title);
        Assert.Equal(["myProfile"], response.BrandingOptions["default"].QuickLinks!);
        Assert.Equal("#FEFEFE", response.BrandingOptions["bcss"].BackgroundColor);
        Assert.Equal("#e3a82b", response.BrandingOptions["bcss"].NavigationSelectedColor);
        Assert.Equal("/images/branding/quartech/logo.png", response.BrandingOptions["bcss"].SecondaryLogoUrl);
        Assert.Equal("Quartech", response.BrandingOptions["bcss"].SecondaryLogoAlt);
        Assert.Equal("/images/branding/bcss/background.png", response.BrandingOptions["bcss"].BackgroundImageUrl);
        Assert.Equal("/images/branding/bcss/BCSS_Dashboard_bg.png", response.BrandingOptions["bcss"].Header!.ImageUrl);
        Assert.Equal("/images/branding/bcss/BCSS_Crest_cmyk_pos.png", response.BrandingOptions["bcss"].Header.LogoUrl);
        Assert.Equal("B.C. Sheriff Service Crest", response.BrandingOptions["bcss"].Header.LogoAlt);
        Assert.Equal("", response.BrandingOptions["bcss"].Header!.PoweredByText);
        Assert.Null(response.BrandingOptions["bcss"].PrimaryColor);
        Assert.Null(response.BrandingOptions["bcss"].QuickLinks);
    }
}
