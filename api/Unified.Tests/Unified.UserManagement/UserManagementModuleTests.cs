using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Common.Mvc;
using Unified.UserManagement;
using Unified.UserManagement.Controllers;
using Unified.UserManagement.Services;

namespace Unified.Tests.UserManagement;

public sealed class UserManagementModuleTests
{
    [Fact]
    public void StartupRegistration_WhenUserManagementModuleEnabled_ExposesUserRoutesAndServices()
    {
        // Arrange
        var services = CreateStartupLikeServices(isEnabled: true, out var provider);
        var actionProvider = provider.GetRequiredService<IActionDescriptorCollectionProvider>();
        var userRoutes = actionProvider
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(action => action.ControllerTypeInfo.AsType() == typeof(UsersController))
            .Select(action => action.AttributeRouteInfo?.Template?.TrimStart('/'))
            .ToArray();

        // Assert
        Assert.Contains(
            services,
            descriptor =>
                descriptor.Lifetime == ServiceLifetime.Scoped
                && descriptor.ServiceType == typeof(IUserService)
                && descriptor.ImplementationType == typeof(UserService)
        );
        Assert.Contains("api/Users", userRoutes);
    }

    [Fact]
    public void StartupRegistration_WhenUserManagementModuleDisabled_DoesNotExposeUserRoutesOrServices()
    {
        // Arrange
        var services = CreateStartupLikeServices(isEnabled: false, out var provider);
        var actionProvider = provider.GetRequiredService<IActionDescriptorCollectionProvider>();
        var userActions = actionProvider
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(action => action.ControllerTypeInfo.AsType() == typeof(UsersController))
            .ToArray();

        // Assert
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IUserService));
        Assert.Empty(userActions);
    }

    private static IServiceCollection CreateStartupLikeServices(bool isEnabled, out ServiceProvider provider)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["FeatureFlags:UserManagement:Enabled"] = isEnabled.ToString() }
            )
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddUserManagementModule(configuration);

        var mvcBuilder = services.AddControllers();
        mvcBuilder.AddApplicationPart(typeof(UsersController).Assembly);
        mvcBuilder.AddConditionalApplicationPart<UsersController>(UserManagementModule.IsModuleEnabled(configuration));

        provider = services.BuildServiceProvider();
        return services;
    }
}
