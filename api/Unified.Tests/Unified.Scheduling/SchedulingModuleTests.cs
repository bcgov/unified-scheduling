using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Authorization.Seeders;
using Unified.Common.Mvc;
using Unified.Common.Seeding;
using Unified.Scheduling;
using Unified.Scheduling.Controllers;
using Unified.Scheduling.Services;
using Unified.Scheduling.Validators;

namespace Unified.Tests.Scheduling;

public sealed class SchedulingModuleTests
{
    [Fact]
    public void StartupRegistration_WhenSchedulingModuleEnabled_ExposesSchedulingCalendarRoute()
    {
        // Arrange
        var services = CreateStartupLikeServices(isEnabled: true, out var provider);
        var actionProvider = provider.GetRequiredService<IActionDescriptorCollectionProvider>();
        var schedulingRoutes = actionProvider
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(action => action.ControllerTypeInfo.AsType() == typeof(SchedulingCalendarController))
            .Select(action => action.AttributeRouteInfo?.Template?.TrimStart('/'))
            .ToArray();

        // Assert
        AssertContainsScopedRegistration<IShiftService, ShiftService>(services);
        AssertContainsScopedSelfRegistration<SchedulingCalendarRequestValidator>(services);
        AssertContainsSingletonInstance<PermissionSeedConfiguration>(
            services,
            SchedulingPermissionSeedData.Configuration
        );
        Assert.Contains("api/scheduling/calendar/events", schedulingRoutes);
    }

    [Fact]
    public void StartupRegistration_WhenSchedulingModuleDisabled_DoesNotExposeSchedulingCalendarRoute()
    {
        // Arrange
        var services = CreateStartupLikeServices(isEnabled: false, out var provider);
        var actionProvider = provider.GetRequiredService<IActionDescriptorCollectionProvider>();
        var schedulingActions = actionProvider
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(action => action.ControllerTypeInfo.AsType() == typeof(SchedulingCalendarController))
            .ToArray();

        // Assert
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IShiftService));
        Assert.Empty(schedulingActions);
    }

    [Fact]
    public void AddSchedulingModule_WhenCalendarIsDisabled_ThrowsDependencyError()
    {
        var configuration = CreateConfiguration(schedulingEnabled: true, calendarEnabled: false);
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddSchedulingModule(configuration));

        Assert.Equal("Scheduling requires the Calendar module to be enabled.", exception.Message);
    }

    private static void AssertContainsScopedRegistration<TService, TImplementation>(IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        Assert.Contains(
            services,
            descriptor =>
                descriptor.Lifetime == ServiceLifetime.Scoped
                && descriptor.ServiceType == typeof(TService)
                && descriptor.ImplementationType == typeof(TImplementation)
        );
    }

    private static void AssertContainsScopedSelfRegistration<TService>(IServiceCollection services)
        where TService : class
    {
        Assert.Contains(
            services,
            descriptor =>
                descriptor.Lifetime == ServiceLifetime.Scoped
                && descriptor.ServiceType == typeof(TService)
                && descriptor.ImplementationType == typeof(TService)
        );
    }

    private static void AssertContainsSingletonInstance<TService>(
        IServiceCollection services,
        TService implementationInstance
    )
        where TService : class
    {
        Assert.Contains(
            services,
            descriptor =>
                descriptor.Lifetime == ServiceLifetime.Singleton
                && descriptor.ServiceType == typeof(TService)
                && ReferenceEquals(descriptor.ImplementationInstance, implementationInstance)
        );
    }

    private static IServiceCollection CreateStartupLikeServices(bool isEnabled, out ServiceProvider provider)
    {
        var configuration = CreateConfiguration(isEnabled, calendarEnabled: true);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSchedulingModule(configuration);

        var mvcBuilder = services.AddControllers();
        mvcBuilder.AddConditionalApplicationPart<ShiftController>(SchedulingModule.IsModuleEnabled(configuration));

        provider = services.BuildServiceProvider();
        return services;
    }

    private static IConfiguration CreateConfiguration(bool schedulingEnabled, bool calendarEnabled) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["FeatureFlags:Scheduling:Enabled"] = schedulingEnabled.ToString(),
                    ["FeatureFlags:Calendar:Enabled"] = calendarEnabled.ToString(),
                }
            )
            .Build();
}
