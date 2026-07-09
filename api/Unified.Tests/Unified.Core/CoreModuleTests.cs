using Microsoft.Extensions.DependencyInjection;
using Unified.Core;
using Unified.Core.Seeders;
using Unified.Core.Services;
using Unified.Core.Services.Lookup;

namespace Unified.Tests.Core;

public sealed class CoreModuleTests
{
    [Fact]
    public void AddCoreModule_WhenCalendarModuleEnabled_RegistersCalendarOwnedLookupStrategies()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCoreModule(calendarModuleEnabled: true, schedulingModuleEnabled: false);

        // Assert
        AssertContainsScopedRegistration<ILookupService, LookupService>(services);
        AssertContainsScopedRegistration<ILookupStrategy, PositionTypeLookupStrategy>(services);
        AssertContainsScopedRegistration<ILookupStrategy, EventTypeLookupStrategy>(services);
        AssertContainsScopedRegistration<ILookupStrategy, EventStatusTypeLookupStrategy>(services);
        AssertContainsScopedRegistration<PositionTypeSeeder, PositionTypeSeeder>(services);
    }

    [Fact]
    public void AddCoreModule_WhenCalendarModuleDisabled_DoesNotRegisterCalendarOwnedLookupStrategies()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCoreModule(calendarModuleEnabled: false, schedulingModuleEnabled: false);

        // Assert
        AssertContainsScopedRegistration<ILookupService, LookupService>(services);
        AssertContainsScopedRegistration<ILookupStrategy, PositionTypeLookupStrategy>(services);
        Assert.DoesNotContain(
            services,
            descriptor =>
                descriptor.Lifetime == ServiceLifetime.Scoped
                && descriptor.ServiceType == typeof(ILookupStrategy)
                && descriptor.ImplementationType == typeof(EventTypeLookupStrategy)
        );
        Assert.DoesNotContain(
            services,
            descriptor =>
                descriptor.Lifetime == ServiceLifetime.Scoped
                && descriptor.ServiceType == typeof(ILookupStrategy)
                && descriptor.ImplementationType == typeof(EventStatusTypeLookupStrategy)
        );
        AssertContainsScopedRegistration<PositionTypeSeeder, PositionTypeSeeder>(services);
    }

    [Fact]
    public void AddCoreModule_WhenSchedulingModuleEnabled_RegistersSchedulingOwnedLookupStrategies()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCoreModule(calendarModuleEnabled: false, schedulingModuleEnabled: true);

        // Assert
        AssertContainsScopedRegistration<ILookupService, LookupService>(services);
        AssertContainsScopedRegistration<ILookupStrategy, PositionTypeLookupStrategy>(services);
        AssertContainsScopedRegistration<ILookupStrategy, AssignmentTypeLookupStrategy>(services);
        Assert.DoesNotContain(
            services,
            descriptor =>
                descriptor.Lifetime == ServiceLifetime.Scoped
                && descriptor.ServiceType == typeof(ILookupStrategy)
                && descriptor.ImplementationType == typeof(EventTypeLookupStrategy)
        );
        AssertContainsScopedRegistration<PositionTypeSeeder, PositionTypeSeeder>(services);
    }

    [Fact]
    public void AddCoreModule_WhenSchedulingModuleDisabled_DoesNotRegisterSchedulingOwnedLookupStrategies()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCoreModule(calendarModuleEnabled: true, schedulingModuleEnabled: false);

        // Assert
        Assert.DoesNotContain(
            services,
            descriptor =>
                descriptor.Lifetime == ServiceLifetime.Scoped
                && descriptor.ServiceType == typeof(ILookupStrategy)
                && descriptor.ImplementationType == typeof(AssignmentTypeLookupStrategy)
        );
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
}
