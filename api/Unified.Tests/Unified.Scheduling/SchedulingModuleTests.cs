using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Authorization.Seeders;
using Unified.Calendar;
using Unified.Common.Mvc;
using Unified.Common.Scheduling;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.LoanOut;
using Unified.LoanOut.Services;
using Unified.Scheduling;
using Unified.Scheduling.Controllers;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;
using Unified.Scheduling.Validators;

namespace Unified.Tests.Scheduling;

public sealed class SchedulingModuleTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SchedulingServices_ResolveWithOptionalLoanOut_InEitherRegistrationOrder(
        bool loanOutEnabled,
        bool loanOutFirst
    )
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["FeatureFlags:Scheduling:Enabled"] = "True",
                    ["FeatureFlags:Calendar:Enabled"] = "True",
                    ["FeatureFlags:Stats:Enabled"] = "True",
                    ["FeatureFlags:LoanOut:Enabled"] = loanOutEnabled.ToString(),
                    ["CalendarDateTime:DefaultTimeZoneId"] = "UTC",
                }
            )
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDbContext<UnifiedDbContext>(options => options.UseSqlite("Data Source=:memory:"));
        services.AddCalendarModule(configuration);
        if (loanOutFirst)
            services.AddLoanOutModule(configuration);
        services.AddSchedulingModule(configuration);
        if (!loanOutFirst)
            services.AddLoanOutModule(configuration);
        if (loanOutEnabled)
        {
            // Re-registering the enabled integration must replace the fallback without duplicate services.
            LoanOutServiceProvider.Register(services);
            services.AddScoped<ISchedulingCalendarEventDataProvider<SchedulingCalendarEventResponse>>(provider =>
                provider.GetRequiredService<ILoanOutCalendarDataProvider>()
            );
        }

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IShiftService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IShiftAssignmentService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAssignmentService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISchedulingCalendarService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISchedulePublishService>());
        var availability = Assert.Single(scope.ServiceProvider.GetServices<ShiftLoanOutAvailabilityService>());
        var markers = Assert.Single(scope.ServiceProvider.GetServices<ShiftLoanOutExceptionMarkerService>());
        var transfers = Assert.Single(scope.ServiceProvider.GetServices<LoanOutShiftTransferService>());
        Assert.Same(availability, scope.ServiceProvider.GetRequiredService<ShiftLoanOutAvailabilityService>());
        Assert.Same(markers, scope.ServiceProvider.GetRequiredService<ShiftLoanOutExceptionMarkerService>());
        Assert.Same(transfers, scope.ServiceProvider.GetRequiredService<LoanOutShiftTransferService>());
        using var secondScope = provider.CreateScope();
        Assert.NotSame(availability, secondScope.ServiceProvider.GetRequiredService<ShiftLoanOutAvailabilityService>());
        Assert.NotSame(markers, secondScope.ServiceProvider.GetRequiredService<ShiftLoanOutExceptionMarkerService>());
        Assert.NotSame(transfers, secondScope.ServiceProvider.GetRequiredService<LoanOutShiftTransferService>());
        var loanOutService = Assert.Single(scope.ServiceProvider.GetServices<ISchedulingLoanOutService>());
        Assert.Same(loanOutService, scope.ServiceProvider.GetRequiredService<ISchedulingLoanOutService>());
        if (loanOutEnabled)
        {
            Assert.IsType<SchedulingLoanOutService>(loanOutService);
            Assert.Same(loanOutService, scope.ServiceProvider.GetRequiredService<SchedulingLoanOutService>());
        }
        else
        {
            Assert.IsType<NoOpSchedulingLoanOutService>(loanOutService);
            Assert.Null(
                scope.ServiceProvider.GetService<
                    ISchedulingCalendarEventDataProvider<SchedulingCalendarEventResponse>
                >()
            );
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoanOutServices_ResolveWithSchedulingDisabled_InEitherRegistrationOrder(bool loanOutFirst)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["FeatureFlags:Scheduling:Enabled"] = "False",
                    ["FeatureFlags:Calendar:Enabled"] = "True",
                    ["FeatureFlags:Stats:Enabled"] = "False",
                    ["FeatureFlags:LoanOut:Enabled"] = "True",
                    ["CalendarDateTime:DefaultTimeZoneId"] = "UTC",
                }
            )
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDbContext<UnifiedDbContext>(options => options.UseSqlite("Data Source=:memory:"));
        services.AddCalendarModule(configuration);
        if (loanOutFirst)
            services.AddLoanOutModule(configuration);
        services.AddSchedulingModule(configuration);
        if (!loanOutFirst)
            services.AddLoanOutModule(configuration);
        LoanOutServiceProvider.Register(services);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ILoanOutService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ILoanOutEntryService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ILoanOutSeriesService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ILoanOutCalendarDataProvider>());
        Assert.Null(scope.ServiceProvider.GetService<IShiftService>());
        var integration = Assert.Single(scope.ServiceProvider.GetServices<ISchedulingLoanOutService>());
        Assert.IsType<NoOpSchedulingLoanOutService>(integration);
        Assert.Same(integration, scope.ServiceProvider.GetRequiredService<ISchedulingLoanOutService>());
        var users = new[] { Guid.NewGuid() };
        var eligible = await integration.GetAssignableUserIdsByOccurrenceAsync(
            users,
            [new ShiftAssignmentLoanOutContext(DateTimeOffset.UtcNow, null)],
            5,
            "UTC",
            TestContext.Current.CancellationToken
        );
        Assert.Equal(users, Assert.Single(eligible));
        Assert.False(
            await integration.TransferLoanedOutUserShift(
                new global::Unified.Db.Models.LoanOut.LoanOutEntry(),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
    }

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
        AssertContainsScopedSelfRegistration<ShiftService>(services);
        AssertContainsScopedFactoryRegistration<IShiftService>(services);
        AssertContainsScopedFactoryRegistration<IShiftPublicationService>(services);
        AssertContainsScopedRegistration<IShiftEntryService, ShiftEntryService>(services);
        AssertContainsScopedRegistration<IShiftSeriesService, ShiftSeriesService>(services);
        AssertContainsScopedRegistration<IAssignmentEntryService, AssignmentEntryService>(services);
        AssertContainsScopedRegistration<IAssignmentSeriesService, AssignmentSeriesService>(services);
        AssertContainsScopedSelfRegistration<SchedulingServiceProvider>(services);
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

    private static void AssertContainsScopedFactoryRegistration<TService>(IServiceCollection services)
        where TService : class
    {
        Assert.Contains(
            services,
            descriptor =>
                descriptor.Lifetime == ServiceLifetime.Scoped
                && descriptor.ServiceType == typeof(TService)
                && descriptor.ImplementationFactory is not null
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
        mvcBuilder.AddApplicationPart(typeof(ShiftController).Assembly);
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
                    ["FeatureFlags:Stats:Enabled"] = bool.TrueString,
                }
            )
            .Build();
}
