using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Calendar;
using Unified.Calendar.Options;
using Unified.Calendar.Seeders;
using Unified.Common.Seeding;
using Unified.Core;
using Unified.Core.Seeders;
using Unified.Db;
using Unified.Stats;
using Unified.Stats.Seeders;
using Unified.UserManagement;
using Unified.UserManagement.Seeders;

namespace Unified.Tests.Common.Seeders;

public sealed class SeederRegistrationTests
{
    [Fact]
    public void UserManagementModule_RegistersExpectedSeedersOnce_WhenAddedTwice()
    {
        var services = CreateServices();
        services.AddUserManagementModule().AddUserManagementModule();

        AssertSeederTypes(
            services,
            typeof(UserSeeder),
            typeof(RoleSeeder),
            typeof(PermissionSeeder),
            typeof(RegionSeeder),
            typeof(LocationSeeder)
        );
    }

    [Fact]
    public void CalendarModule_RegistersExpectedSeeders()
    {
        var services = CreateServices();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{CalendarSeedDataOptions.SectionName}:HolidaysFilePath"] = "SeedData/bc-holidays.json",
                }
            )
            .Build();
        services.AddCalendarModule(configuration);

        AssertSeederTypes(services, typeof(EventTypeSeeder), typeof(EventStatusTypeSeeder), typeof(HolidayEventSeeder));
    }

    [Fact]
    public void CoreModule_RegistersExpectedSeeder()
    {
        var services = CreateServices();
        services.AddCoreModule();

        AssertSeederTypes(services, typeof(PositionTypeSeeder));
    }

    [Fact]
    public void StatsModule_RegistersExpectedSeeders()
    {
        var services = CreateServices();
        services.AddStatsModule();

        AssertSeederTypes(
            services,
            typeof(StatGroupSeeder),
            typeof(StatCategorySeeder),
            typeof(SubCategorySeeder),
            typeof(StatMetricSeeder),
            typeof(SubCategoryMetricSeeder)
        );
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    private static void AssertSeederTypes(IServiceCollection services, params Type[] expectedTypes)
    {
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var actualTypes = scope
            .ServiceProvider.GetServices<SeederBase<UnifiedDbContext>>()
            .Select(seeder => seeder.GetType())
            .ToArray();

        Assert.Equal(
            expectedTypes.OrderBy(type => type.FullName, StringComparer.Ordinal),
            actualTypes.OrderBy(type => type.FullName, StringComparer.Ordinal)
        );
        foreach (var expectedType in expectedTypes)
            Assert.NotNull(scope.ServiceProvider.GetService(expectedType));
    }
}
