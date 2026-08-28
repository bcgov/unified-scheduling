using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Common.Mvc;
using Unified.Stats;
using Unified.Stats.Controllers;
using Unified.Stats.Services;

namespace Unified.Tests.Stats;

public sealed class StatsModuleTests
{
    [Fact]
    public void StartupRegistration_WhenStatsModuleEnabled_ExposesStatsRoutesAndServices()
    {
        // Arrange
        var services = CreateStartupLikeServices(isEnabled: true, out var provider);
        var actionProvider = provider.GetRequiredService<IActionDescriptorCollectionProvider>();
        var statsRoutes = actionProvider
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(action => action.ControllerTypeInfo.AsType() == typeof(StatGroupsController))
            .Select(action => action.AttributeRouteInfo?.Template?.TrimStart('/'))
            .ToArray();

        // Assert
        Assert.Contains(
            services,
            descriptor =>
                descriptor.Lifetime == ServiceLifetime.Scoped
                && descriptor.ServiceType == typeof(IStatGroupService)
                && descriptor.ImplementationType == typeof(StatGroupService)
        );
        Assert.Contains("api/stats/groups", statsRoutes);
    }

    [Fact]
    public void StartupRegistration_WhenStatsModuleDisabled_DoesNotExposeStatsRoutesOrServices()
    {
        // Arrange
        var services = CreateStartupLikeServices(isEnabled: false, out var provider);
        var actionProvider = provider.GetRequiredService<IActionDescriptorCollectionProvider>();
        var statsActions = actionProvider
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(action => action.ControllerTypeInfo.AsType() == typeof(StatGroupsController))
            .ToArray();

        // Assert
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IStatGroupService));
        Assert.Empty(statsActions);
    }

    private static IServiceCollection CreateStartupLikeServices(bool isEnabled, out ServiceProvider provider)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["FeatureFlags:Stats:Enabled"] = isEnabled.ToString() }
            )
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddStatsModule(configuration);

        var mvcBuilder = services.AddControllers();
        mvcBuilder.AddApplicationPart(typeof(StatGroupsController).Assembly);
        mvcBuilder.AddConditionalApplicationPart<StatGroupsController>(StatsModule.IsModuleEnabled(configuration));

        provider = services.BuildServiceProvider();
        return services;
    }
}
