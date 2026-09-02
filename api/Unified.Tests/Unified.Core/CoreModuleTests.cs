using Microsoft.Extensions.DependencyInjection;
using Unified.Core;
using Unified.Core.Services;
using Unified.Core.Services.Lookup;

namespace Unified.Tests.Core;

public sealed class CoreModuleTests
{
    [Fact]
    public void AddCoreModule_RegistersLookupStrategies()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCoreModule();

        // Assert
        AssertContainsScopedRegistration<ILookupService, LookupService>(services);
        AssertContainsScopedRegistration<ILookupStrategy, PositionTypeLookupStrategy>(services);
        AssertContainsScopedRegistration<ILookupStrategy, EventTypeLookupStrategy>(services);
        AssertContainsScopedRegistration<ILookupStrategy, EventStatusTypeLookupStrategy>(services);
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
