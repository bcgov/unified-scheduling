using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.Leave;
using Unified.Leave.Models;
using Unified.Leave.Services;

namespace Unified.Tests.Leave;

public sealed class LeaveModuleTests
{
    [Theory]
    [InlineData("True", true)]
    [InlineData("False", false)]
    public void IsModuleEnabled_ReflectsLeaveEnabledFlag(string value, bool expected)
    {
        Assert.Equal(expected, LeaveModule.IsModuleEnabled(CreateConfiguration(value)));
    }

    [Fact]
    public void IsModuleEnabled_WhenFlagMissing_ReturnsFalse()
    {
        Assert.False(LeaveModule.IsModuleEnabled(CreateConfiguration(null)));
    }

    [Fact]
    public void AddLeaveModule_WhenDisabled_DoesNotRegisterServices()
    {
        var services = new ServiceCollection();

        services.AddLeaveModule(CreateConfiguration("False", "False"));

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ISchedulingLeaveService));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ILeaveTypeService));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ILeaveCalendarDataProvider));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(LeaveSeriesMaterializationHandler));
    }

    [Theory]
    [InlineData("False")]
    [InlineData(null)]
    public void AddLeaveModule_WhenEnabledWithoutCalendar_Throws(string? calendarEnabled)
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddLeaveModule(CreateConfiguration("True", calendarEnabled))
        );

        Assert.Equal("Leave requires the Calendar module to be enabled.", exception.Message);
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ISchedulingLeaveService));
    }

    [Fact]
    public void AddLeaveModule_WhenEnabled_RegistersScopedServices()
    {
        var services = new ServiceCollection();

        services.AddLeaveModule(CreateConfiguration("True"));

        AssertScoped<ISchedulingLeaveService, SchedulingLeaveService>(services);
        AssertScoped<ILeaveTypeService, LeaveTypeService>(services);
        AssertScoped<ILeaveCalendarDataProvider, LeaveCalendarDataProvider>(services);
    }

    [Fact]
    public void AddLeaveModule_WhenEnabledWithoutCalendarRegistrations_DoesNotRegisterUnavailableDependencies()
    {
        var services = new ServiceCollection();

        services.AddLeaveModule(CreateConfiguration("True"));

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(CalendarLifecycleService));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IRecurrenceRuleValidator));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IEventSeriesMaterializationService)
        );
    }

    [Fact]
    public async Task AddLeaveModule_WhenEnabled_ResolvesHandlerAsConcreteAndInterfaceWithSameInstance()
    {
        await using var database = await LeaveTestDatabase.CreateAsync();
        var services = new ServiceCollection();
        services.AddSingleton<UnifiedDbContext>(database.Db);
        services.AddLeaveModule(CreateConfiguration("True"));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var concrete = scope.ServiceProvider.GetRequiredService<LeaveSeriesMaterializationHandler>();
        var viaInterface = scope.ServiceProvider.GetRequiredService<
            IEventSeriesMaterializationHandler<LeaveSeriesMaterializationContext>
        >();

        Assert.Same(concrete, viaInterface);
    }

    private static void AssertScoped<TService, TImplementation>(IServiceCollection services) =>
        Assert.Contains(
            services,
            d =>
                d.Lifetime == ServiceLifetime.Scoped
                && d.ServiceType == typeof(TService)
                && d.ImplementationType == typeof(TImplementation)
        );

    private static IConfiguration CreateConfiguration(string? enabled, string? calendarEnabled = "True") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["FeatureFlags:Leave:Enabled"] = enabled,
                    ["FeatureFlags:Calendar:Enabled"] = calendarEnabled,
                }
            )
            .Build();
}
