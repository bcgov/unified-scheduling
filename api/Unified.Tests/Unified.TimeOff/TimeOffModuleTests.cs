using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.TimeOff;
using Unified.TimeOff.Models;
using Unified.TimeOff.Services;

namespace Unified.Tests.TimeOff;

public sealed class TimeOffModuleTests
{
    [Theory]
    [InlineData("True", true)]
    [InlineData("False", false)]
    public void IsModuleEnabled_ReflectsLeaveEnabledFlag(string value, bool expected)
    {
        Assert.Equal(expected, TimeOffModule.IsModuleEnabled(CreateConfiguration(value)));
    }

    [Fact]
    public void IsModuleEnabled_WhenFlagMissing_ReturnsFalse()
    {
        Assert.False(TimeOffModule.IsModuleEnabled(CreateConfiguration(null)));
    }

    [Fact]
    public void AddTimeOffModule_WhenDisabled_DoesNotRegisterServices()
    {
        var services = new ServiceCollection();

        services.AddTimeOffModule(CreateConfiguration("False"));

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ISchedulingTimeOffService));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ILeaveTypeService));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ITimeOffCalendarDataProvider));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(TimeOffSeriesMaterializationHandler));
    }

    [Fact]
    public void AddTimeOffModule_WhenEnabled_RegistersScopedServices()
    {
        var services = new ServiceCollection();

        services.AddTimeOffModule(CreateConfiguration("True"));

        AssertScoped<ISchedulingTimeOffService, SchedulingTimeOffService>(services);
        AssertScoped<ILeaveTypeService, LeaveTypeService>(services);
        AssertScoped<ITimeOffCalendarDataProvider, TimeOffCalendarDataProvider>(services);
    }

    [Fact]
    public async Task AddTimeOffModule_WhenEnabled_ResolvesHandlerAsConcreteAndInterfaceWithSameInstance()
    {
        await using var database = await TimeOffTestDatabase.CreateAsync();
        var services = new ServiceCollection();
        services.AddSingleton<UnifiedDbContext>(database.Db);
        services.AddTimeOffModule(CreateConfiguration("True"));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var concrete = scope.ServiceProvider.GetRequiredService<TimeOffSeriesMaterializationHandler>();
        var viaInterface = scope.ServiceProvider.GetRequiredService<
            IEventSeriesMaterializationHandler<TimeOffSeriesMaterializationContext>
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

    private static IConfiguration CreateConfiguration(string? enabled) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                enabled is null
                    ? new Dictionary<string, string?>()
                    : new Dictionary<string, string?> { ["FeatureFlags:TimeOff:Enabled"] = enabled }
            )
            .Build();
}
