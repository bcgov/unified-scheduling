using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
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
        var services = new ServiceCollection();
        services.AddLogging();

        var mvcBuilder = services.AddControllers();
        mvcBuilder.AddSchedulingApplicationPart(isEnabled);

        if (isEnabled)
        {
            services.AddSchedulingModule();
        }

        provider = services.BuildServiceProvider();
        return services;
    }
}
