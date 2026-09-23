using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Common.Jobs;
using Unified.Common.Contracts;
using Unified.Common.Events;
using Unified.Common.Mvc;
using Unified.Db;
using Unified.Tests.TestHelpers;
using Unified.Training;
using Unified.Training.Controllers;
using Unified.Training.Jobs;
using Unified.Training.Handlers;
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
        using var providerLifetime = provider;
        var actionProvider = provider.GetRequiredService<IActionDescriptorCollectionProvider>();
        var userTrainingRoutes = actionProvider
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(action => action.ControllerTypeInfo.AsType() == typeof(UserTrainingController))
            .Select(action => action.AttributeRouteInfo?.Template?.TrimStart('/'))
            .ToArray();

        // Assert
        AssertContainsScopedRegistration<IUserTrainingService, UserTrainingService>(services);
        AssertContainsScopedRegistration<IUserTrainingExpiryNotificationService, UserTrainingExpiryNotificationService>(
            services
        );
        AssertContainsScopedRegistration<IRecurringJob, UserTrainingExpiryNoticeRecurringJob>(services);
        AssertContainsScopedRegistration<
            IEntitySideEffect<UserCreatedSignal>,
            AssignMandatoryTrainingOnUserCreationHandler
        >(services);
        using var scope = provider.CreateScope();
        // Handlers are resolved from DI and use the scoped context.
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<UnifiedDbContext>());
        var handler = Assert.IsType<AssignMandatoryTrainingOnUserCreationHandler>(
            Assert.Single(scope.ServiceProvider.GetServices<IEntitySideEffect<UserCreatedSignal>>())
        );
        Assert.Same(handler, Assert.Single(scope.ServiceProvider.GetServices<IEntitySideEffect<UserCreatedSignal>>()));
        using var otherScope = provider.CreateScope();
        Assert.NotSame(
            handler,
            Assert.Single(otherScope.ServiceProvider.GetServices<IEntitySideEffect<UserCreatedSignal>>())
        );
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
        using var providerLifetime = provider;
        var actionProvider = provider.GetRequiredService<IActionDescriptorCollectionProvider>();
        var userTrainingActions = actionProvider
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(action => action.ControllerTypeInfo.AsType() == typeof(UserTrainingController))
            .ToArray();

        // Assert
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IUserTrainingService));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IUserTrainingExpiryNotificationService)
        );
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IRecurringJob));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IEntitySideEffect<UserCreatedSignal>)
        );
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(AssignMandatoryTrainingOnUserCreationHandler)
        );
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
        services.AddScoped<UnifiedDbContext>(sp => new SqliteTestUnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite("Data Source=:memory:").Options
        ));

        var mvcBuilder = services.AddControllers();
        mvcBuilder.AddApplicationPart(typeof(UserTrainingController).Assembly);
        mvcBuilder.AddConditionalApplicationPart<UserTrainingController>(TrainingModule.IsModuleEnabled(configuration));

        provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        return services;
    }

    private static IConfiguration CreateConfiguration(bool trainingEnabled) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["FeatureFlags:Training:Enabled"] = trainingEnabled.ToString() }
            )
            .Build();
}
