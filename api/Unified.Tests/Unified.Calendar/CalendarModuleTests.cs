using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.Calendar;
using Unified.Calendar.Controllers;
using Unified.Calendar.Options;
using Unified.Calendar.Seeders;
using Unified.Calendar.Services;
using Unified.Calendar.Validators;

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
        AssertContainsScopedRegistration<ICalendarEventService, CalendarEventService>(services);
        AssertContainsScopedSelfRegistration<EventTypeSeeder>(services);
        AssertContainsScopedSelfRegistration<EventStatusTypeSeeder>(services);
        AssertContainsScopedSelfRegistration<HolidayEventSeeder>(services);
        AssertContainsScopedSelfRegistration<CalendarDataRequestValidator>(services);
        Assert.Contains("api/calendar/events", calendarRoutes);
        Assert.Equal(
            "SeedData/bc-holidays.json",
            provider.GetRequiredService<IOptions<CalendarSeedDataOptions>>().Value.HolidaysFilePath
        );
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
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{CalendarSeedDataOptions.SectionName}:HolidaysFilePath"] = "SeedData/bc-holidays.json",
                    [$"{CalendarDateTimeOptions.SectionName}:DefaultTimeZoneId"] = "America/Toronto",
                }
            )
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        var mvcBuilder = services.AddControllers();
        mvcBuilder.AddCalendarApplicationPart(isEnabled);

        if (isEnabled)
        {
            services.AddCalendarModule(configuration);
        }

        provider = services.BuildServiceProvider();
        return services;
    }
}
