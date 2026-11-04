using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Calendar.Services;
using Unified.LoanOut;
using Unified.LoanOut.Models;
using Unified.LoanOut.Services;
using Unified.LoanOut.Validators;

namespace Unified.Tests.LoanOut;

public sealed class LoanOutModuleTests
{
    [Theory]
    [InlineData("True", true)]
    [InlineData("False", false)]
    public void IsModuleEnabled_ReflectsLoanOutEnabledFlag(string value, bool expected)
    {
        Assert.Equal(expected, LoanOutModule.IsModuleEnabled(CreateConfiguration(value)));
    }

    [Fact]
    public void IsModuleEnabled_WhenFlagMissing_ReturnsFalse()
    {
        Assert.False(LoanOutModule.IsModuleEnabled(CreateConfiguration(null)));
    }

    [Fact]
    public void AddLoanOutModule_WhenDisabled_DoesNotRegisterServices()
    {
        var services = new ServiceCollection();

        services.AddLoanOutModule(CreateConfiguration("False", "False"));

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ILoanOutService));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ILoanOutEntryService));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ILoanOutSeriesService));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ILoanOutCalendarDataProvider));
    }

    [Theory]
    [InlineData("False")]
    [InlineData(null)]
    public void AddLoanOutModule_WhenEnabledWithoutCalendar_Throws(string? calendarEnabled)
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddLoanOutModule(CreateConfiguration("True", calendarEnabled))
        );

        Assert.Equal("LoanOut requires the Calendar module to be enabled.", exception.Message);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ILoanOutService));
    }

    [Fact]
    public void AddLoanOutModule_WhenEnabled_RegistersScopedServicesAndMaterializationHandler()
    {
        var services = new ServiceCollection();

        services.AddLoanOutModule(CreateConfiguration("True"));

        AssertScoped<ILoanOutService, LoanOutService>(services);
        AssertScoped<ILoanOutEntryService, LoanOutEntryService>(services);
        AssertScoped<ILoanOutSeriesService, LoanOutSeriesService>(services);
        AssertScoped<ILoanOutCalendarDataProvider, LoanOutCalendarDataProvider>(services);
        AssertScoped<
            IEventSeriesMaterializationHandler<LoanOutSeriesMaterializationContext>,
            LoanOutSeriesMaterializationHandler
        >(services);
        AssertScopedSelf<LoanOutSeriesMaterializationHandler>(services);
        AssertScopedSelf<LoanOutRequestValidator>(services);
        AssertScopedSelf<LoanOutSeriesRequestValidator>(services);
        AssertScopedSelf<LoanOutUserSearchRequestValidator>(services);
    }

    private static void AssertScoped<TService, TImplementation>(IServiceCollection services)
        where TService : class
        where TImplementation : class, TService =>
        Assert.Contains(
            services,
            descriptor =>
                descriptor.Lifetime == ServiceLifetime.Scoped
                && descriptor.ServiceType == typeof(TService)
                && (
                    descriptor.ImplementationType == typeof(TImplementation)
                    || descriptor.ImplementationFactory is not null
                )
        );

    private static void AssertScopedSelf<TService>(IServiceCollection services)
        where TService : class =>
        Assert.Contains(
            services,
            descriptor =>
                descriptor.Lifetime == ServiceLifetime.Scoped
                && descriptor.ServiceType == typeof(TService)
                && descriptor.ImplementationType == typeof(TService)
        );

    private static IConfiguration CreateConfiguration(string? enabled, string? calendarEnabled = "True") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["FeatureFlags:LoanOut:Enabled"] = enabled,
                    ["FeatureFlags:Calendar:Enabled"] = calendarEnabled,
                }
            )
            .Build();
}
