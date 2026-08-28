using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Unified.Calendar;
using Unified.Calendar.Controllers;
using Unified.Calendar.Holidays;
using Unified.Calendar.Options;
using Unified.Calendar.Seeders;
using Unified.Calendar.Services;
using Unified.Calendar.Validators;
using Unified.Common.Mvc;
using Unified.Common.Seeding;
using Unified.Common.Time;
using Unified.Db;

namespace Unified.Tests.Calendar;

public sealed class CalendarModuleTests
{
    [Fact]
    public void StartupRegistration_WhenCalendarModuleEnabled_ExposesCalendarRouteAndServices()
    {
        // Arrange
        var services = CreateStartupLikeServices(isEnabled: true, out var provider);

        var actionProvider = provider.GetRequiredService<IActionDescriptorCollectionProvider>();
        var calendarRoutes = actionProvider
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(action => action.ControllerTypeInfo.AsType() == typeof(CalendarController))
            .Select(action => action.AttributeRouteInfo?.Template?.TrimStart('/'))
            .ToArray();

        // Assert
        AssertContainsSingletonRegistration<IStatutoryHolidayCalculator, BcStatutoryHolidayCalculator>(services);
        AssertContainsSingletonSelfRegistration<StatutoryHolidayCalendarDataProvider>(services);
        AssertContainsScopedRegistration<ICalendarTimeZoneResolver, CalendarTimeZoneResolver>(services);
        AssertContainsScopedRegistration<ICalendarEventService, CalendarEventService>(services);
        AssertContainsScopedRegistration<SeederBase<UnifiedDbContext>, EventTypeSeeder>(services);
        AssertContainsScopedRegistration<SeederBase<UnifiedDbContext>, EventStatusTypeSeeder>(services);

        AssertContainsScopedSelfRegistration<CalendarDataRequestValidator>(services);
        Assert.Contains("api/calendar/events", calendarRoutes);
        Assert.Equal(
            "America/Toronto",
            provider.GetRequiredService<IOptions<CalendarDateTimeOptions>>().Value.DefaultTimeZoneId
        );
    }

    [Fact]
    public void StartupRegistration_WhenCalendarModuleDisabled_DoesNotExposeCalendarRouteOrServices()
    {
        // Arrange
        var services = CreateStartupLikeServices(isEnabled: false, out var provider);
        var actionProvider = provider.GetRequiredService<IActionDescriptorCollectionProvider>();
        var calendarActions = actionProvider
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(action => action.ControllerTypeInfo.AsType() == typeof(CalendarController))
            .ToArray();

        // Assert
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ICalendarEventService));
        Assert.Empty(calendarActions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Not/AZone")]
    public async Task StartupValidation_WhenDefaultTimeZoneIsMissingOrInvalid_Throws(string? defaultTimeZoneId)
    {
        using var host = new HostBuilder()
            .ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        [$"{CalendarDateTimeOptions.SectionName}:DefaultTimeZoneId"] = defaultTimeZoneId,
                        ["FeatureFlags:Calendar:Enabled"] = "true",
                    }
                )
            )
            .ConfigureServices((context, services) => services.AddCalendarModule(context.Configuration))
            .Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() =>
            host.StartAsync(TestContext.Current.CancellationToken)
        );

        Assert.Contains(exception.Failures, failure => failure.Contains("DefaultTimeZoneId", StringComparison.Ordinal));
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

    private static void AssertContainsSingletonRegistration<TService, TImplementation>(IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        Assert.Contains(
            services,
            descriptor =>
                descriptor.Lifetime == ServiceLifetime.Singleton
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

    private static void AssertContainsSingletonSelfRegistration<TService>(IServiceCollection services)
        where TService : class
    {
        Assert.Contains(
            services,
            descriptor =>
                descriptor.Lifetime == ServiceLifetime.Singleton
                && descriptor.ServiceType == typeof(TService)
                && descriptor.ImplementationType == typeof(TService)
        );
    }

    private static IServiceCollection CreateStartupLikeServices(
        bool isEnabled,
        out ServiceProvider provider,
        string? defaultTimeZoneId = "America/Toronto"
    )
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{CalendarDateTimeOptions.SectionName}:DefaultTimeZoneId"] = defaultTimeZoneId,
                    ["FeatureFlags:Calendar:Enabled"] = isEnabled.ToString(),
                }
            )
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<ITimeZoneService, TimeZoneService>();
        services.AddCalendarModule(configuration);

        var mvcBuilder = services.AddControllers();
        mvcBuilder.AddApplicationPart(typeof(CalendarController).Assembly);
        mvcBuilder.AddConditionalApplicationPart<CalendarController>(CalendarModule.IsModuleEnabled(configuration));

        provider = services.BuildServiceProvider();
        return services;
    }
}
