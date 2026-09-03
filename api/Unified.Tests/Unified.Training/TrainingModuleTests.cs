using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Common.Mvc;
using Unified.Training;
using Unified.Training.Controllers;
using Unified.Training.Services;
using Unified.Training.Services.Lookup;
using Unified.Training.Validators;

namespace Unified.Tests.Training;

public sealed class TrainingModuleTests
{
    [Fact]
    public void StartupRegistration_WhenTrainingModuleEnabled_ExposesTrainingRoutesAndServices()
    {
        // Arrange
        var services = CreateStartupLikeServices(isEnabled: true, out var provider);
        var actionProvider = provider.GetRequiredService<IActionDescriptorCollectionProvider>();
        var userTrainingRoutes = actionProvider
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(action => action.ControllerTypeInfo.AsType() == typeof(UserTrainingController))
            .Select(action => action.AttributeRouteInfo?.Template?.TrimStart('/'))
            .ToArray();

        // Assert
        AssertContainsScopedRegistration<IUserTrainingService, UserTrainingService>(services);
        AssertContainsScopedRegistration<ITrainingLookupStrategy, TrainingLookupStrategy>(services);
        AssertContainsScopedSelfRegistration<TrainingLookupRequestValidator>(services);
        AssertContainsScopedSelfRegistration<UserTrainingRequestValidator>(services);
        Assert.Contains("api/training/user-trainings", userTrainingRoutes);
    }

    [Fact]
    public void StartupRegistration_WhenTrainingModuleDisabled_DoesNotExposeTrainingRoutesOrServices()
    {
        // Arrange
        var services = CreateStartupLikeServices(isEnabled: false, out var provider);
        var actionProvider = provider.GetRequiredService<IActionDescriptorCollectionProvider>();
        var userTrainingActions = actionProvider
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(action => action.ControllerTypeInfo.AsType() == typeof(UserTrainingController))
            .ToArray();

        // Assert
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IUserTrainingService));
        Assert.Empty(userTrainingActions);
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

    private static IServiceCollection CreateStartupLikeServices(bool isEnabled, out ServiceProvider provider)
    {
        var configuration = CreateConfiguration(isEnabled);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddTrainingModule(configuration);

        var mvcBuilder = services.AddControllers();
        mvcBuilder.AddApplicationPart(typeof(UserTrainingController).Assembly);
        mvcBuilder.AddConditionalApplicationPart<UserTrainingController>(TrainingModule.IsModuleEnabled(configuration));

        provider = services.BuildServiceProvider();
        return services;
    }

    private static IConfiguration CreateConfiguration(bool trainingEnabled) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["FeatureFlags:Training:Enabled"] = trainingEnabled.ToString() }
            )
            .Build();
}
