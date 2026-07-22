using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Api.Services;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.UserManagement;
using Unified.UserManagement.Seeders;

namespace Unified.Tests.UserManagement.Seeders;

public sealed class SeedDataCompositionTests
{
    [Fact]
    public void AddConfiguredSeedData_SheriffRegionLocationDataSet_RegistersRegionsAndLocations()
    {
        var composition = GetComposition(SeedDataComposition.SheriffRegionLocationDataSet);

        Assert.Equal([SeedDataComposition.SheriffRegionLocationDataSet], composition.DataSets);
        Assert.Equal(
            [SeedDataComposition.SheriffRegionLocationDataSet],
            composition.RegionConfigurations.Select(x => x.Source)
        );
        Assert.Equal(
            [SeedDataComposition.SheriffRegionLocationDataSet],
            composition.LocationConfigurations.Select(x => x.Source)
        );
    }

    [Fact]
    public void AddConfiguredSeedData_EmptyDataSets_DoesNotRegisterInheritedData()
    {
        var composition = GetComposition();

        Assert.Empty(composition.DataSets);
        Assert.Empty(composition.RegionConfigurations);
        Assert.Empty(composition.LocationConfigurations);
        Assert.Empty(composition.UserConfigurations);
        Assert.Empty(composition.RoleConfigurations);
        Assert.Empty(composition.PermissionConfigurations);
    }

    [Fact]
    public void AddConfiguredSeedData_SelectedDataSets_RegistersExactlySelectedContributions()
    {
        var composition = GetComposition(
            SeedDataComposition.PlatformSystemUserDataSet,
            SeedDataComposition.DefaultRolesDataSet,
            SeedDataComposition.UserManagementPermissionsDataSet
        );

        Assert.Equal(3, composition.DataSets.Count);
        Assert.Equal(
            [SeedDataComposition.PlatformSystemUserDataSet],
            composition.UserConfigurations.Select(x => x.Source)
        );
        Assert.Equal([SeedDataComposition.DefaultRolesDataSet], composition.RoleConfigurations.Select(x => x.Source));
        Assert.Equal(
            [SeedDataComposition.UserManagementPermissionsDataSet],
            composition.PermissionConfigurations.Select(x => x.Source)
        );
    }

    [Fact]
    public void AddConfiguredSeedData_UnknownDataSet_ThrowsConfigurationError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddConfiguredSeedData(BuildConfiguration("missing-data-set"))
        );

        Assert.Contains("missing-data-set", exception.Message);
        Assert.Contains("data set", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(SeedDataComposition.StatsPermissionsDataSet, "StatsModule")]
    [InlineData(SeedDataComposition.TrainingPermissionsDataSet, "TrainingModule")]
    public void AddConfiguredSeedData_FeatureDataSetDisabled_ThrowsConfigurationError(string dataSet, string feature)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddConfiguredSeedData(BuildConfiguration(dataSet))
        );

        Assert.Contains(dataSet, exception.Message);
        Assert.Contains(feature, exception.Message);
    }

    [Theory]
    [InlineData(SeedDataComposition.StatsPermissionsDataSet, "StatsModule")]
    [InlineData(SeedDataComposition.TrainingPermissionsDataSet, "TrainingModule")]
    public void AddConfiguredSeedData_FeatureEnabledWithoutPermissionDataSet_ThrowsConfigurationError(
        string dataSet,
        string feature
    )
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddConfiguredSeedData(BuildConfiguration([], feature))
        );

        Assert.Contains(dataSet, exception.Message);
        Assert.Contains(feature, exception.Message);
    }

    [Theory]
    [InlineData(SeedDataComposition.StatsPermissionsDataSet, "StatsModule")]
    [InlineData(SeedDataComposition.TrainingPermissionsDataSet, "TrainingModule")]
    public void AddConfiguredSeedData_FeatureDataSetEnabled_RegistersContribution(string dataSet, string feature)
    {
        var composition = GetComposition(dataSet, feature);

        Assert.Equal([dataSet], composition.PermissionConfigurations.Select(x => x.Source));
    }

    private static SeedComposition GetComposition(params string[] dataSets) => GetComposition(dataSets, null);

    private static SeedComposition GetComposition(string dataSet, string feature) => GetComposition([dataSet], feature);

    private static SeedComposition GetComposition(string[] dataSets, string? enabledFeature)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUserManagementModule();
        services.AddConfiguredSeedData(BuildConfiguration(dataSets, enabledFeature));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        return new SeedComposition(
            scope.ServiceProvider.GetRequiredService<ResolvedSeedDataConfiguration>().DataSets,
            scope.ServiceProvider.GetServices<RegionSeedConfiguration>().ToArray(),
            scope.ServiceProvider.GetServices<LocationSeedConfiguration>().ToArray(),
            scope.ServiceProvider.GetServices<UserSeedConfiguration>().ToArray(),
            scope.ServiceProvider.GetServices<RoleSeedConfiguration>().ToArray(),
            scope.ServiceProvider.GetServices<PermissionSeedConfiguration>().ToArray()
        );
    }

    private static IConfiguration BuildConfiguration(string dataSet) => BuildConfiguration([dataSet], null);

    private static IConfiguration BuildConfiguration(string[] dataSets, string? enabledFeature = null)
    {
        var values = new Dictionary<string, string?>();
        for (var index = 0; index < dataSets.Length; index++)
            values[$"SeedData:DataSets:{index}"] = dataSets[index];
        if (enabledFeature is not null)
            values[$"FeatureFlags:{enabledFeature}"] = "true";
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private sealed record SeedComposition(
        IReadOnlyList<string> DataSets,
        RegionSeedConfiguration[] RegionConfigurations,
        LocationSeedConfiguration[] LocationConfigurations,
        UserSeedConfiguration[] UserConfigurations,
        RoleSeedConfiguration[] RoleConfigurations,
        PermissionSeedConfiguration[] PermissionConfigurations
    );
}
