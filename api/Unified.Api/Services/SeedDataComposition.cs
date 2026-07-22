using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Common.Seeding;
using Unified.Stats;
using Unified.Training;
using Unified.UserManagement;

namespace Unified.Api.Services;

/// <summary>
/// Registers the seed-data contributions selected by deployment configuration.
/// Application configuration selects data sets; modules own executable seeder registration.
/// </summary>
public static class SeedDataComposition
{
    public const string SheriffRegionLocationDataSet = "sheriff-region-location-data";
    public const string PlatformSystemUserDataSet = "platform-system-user";
    public const string DefaultRolesDataSet = "default-roles";
    public const string UserManagementPermissionsDataSet = "user-management-permissions";
    public const string StatsPermissionsDataSet = "stats-permissions";
    public const string TrainingPermissionsDataSet = "training-permissions";

    private static readonly IReadOnlyDictionary<string, SeedDataSetDescriptor> DataSetCatalog = new Dictionary<
        string,
        SeedDataSetDescriptor
    >(StringComparer.OrdinalIgnoreCase)
    {
        [PlatformSystemUserDataSet] = new(
            PlatformSystemUserDataSet,
            services =>
                services.AddSingleton(
                    new UserSeedConfiguration
                    {
                        Source = PlatformSystemUserDataSet,
                        Users = PlatformSystemUserSeedData.Users,
                    }
                )
        ),
        [DefaultRolesDataSet] = new(
            DefaultRolesDataSet,
            services =>
                services.AddSingleton(
                    new RoleSeedConfiguration { Source = DefaultRolesDataSet, Roles = DefaultRoleSeedData.Roles }
                )
        ),
        [UserManagementPermissionsDataSet] = new(
            UserManagementPermissionsDataSet,
            services =>
                services.AddSingleton(
                    new PermissionSeedConfiguration
                    {
                        Source = UserManagementPermissionsDataSet,
                        Permissions = UserManagementPermissionSeedData.Definitions,
                    }
                )
        ),
        [StatsPermissionsDataSet] = new(
            StatsPermissionsDataSet,
            services =>
                services.AddSingleton(
                    new PermissionSeedConfiguration
                    {
                        Source = StatsPermissionsDataSet,
                        Permissions = StatsPermissionSeedData.Definitions,
                    }
                ),
            RequiredFeature: "StatsModule",
            AvailableWhen: configuration => configuration.GetValue<bool>("FeatureFlags:StatsModule")
        ),
        [TrainingPermissionsDataSet] = new(
            TrainingPermissionsDataSet,
            services =>
                services.AddSingleton(
                    new PermissionSeedConfiguration
                    {
                        Source = TrainingPermissionsDataSet,
                        Permissions = TrainingPermissionSeedData.Definitions,
                    }
                ),
            RequiredFeature: "TrainingModule",
            AvailableWhen: configuration => configuration.GetValue<bool>("FeatureFlags:TrainingModule")
        ),
        [SheriffRegionLocationDataSet] = new(
            SheriffRegionLocationDataSet,
            services =>
            {
                services.AddSingleton(
                    new RegionSeedConfiguration
                    {
                        Source = SheriffRegionLocationDataSet,
                        Regions = SheriffRegionSeedData.Regions,
                    }
                );
                services.AddSingleton(
                    new LocationSeedConfiguration
                    {
                        Source = SheriffRegionLocationDataSet,
                        Locations = SheriffLocationSeedData.Locations,
                    }
                );
            }
        ),
    };

    public static IServiceCollection AddConfiguredSeedData(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var options = configuration.GetSection(SeedDataOptions.SectionName).Get<SeedDataOptions>() ?? new();

        var selectedDataSets = options.DataSets.ToArray();
        var duplicateDataSets = selectedDataSets
            .GroupBy(dataSet => dataSet, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateDataSets.Length > 0)
        {
            throw new InvalidOperationException(
                $"SeedData:DataSets contains duplicate entries: {string.Join(", ", duplicateDataSets)}"
            );
        }

        if (
            configuration.GetValue<bool>("FeatureFlags:StatsModule")
            && !selectedDataSets.Contains(StatsPermissionsDataSet, StringComparer.OrdinalIgnoreCase)
        )
        {
            throw new InvalidOperationException(
                $"FeatureFlags:StatsModule requires seed-data set '{StatsPermissionsDataSet}' to be selected."
            );
        }

        if (
            configuration.GetValue<bool>("FeatureFlags:TrainingModule")
            && !selectedDataSets.Contains(TrainingPermissionsDataSet, StringComparer.OrdinalIgnoreCase)
        )
        {
            throw new InvalidOperationException(
                $"FeatureFlags:TrainingModule requires seed-data set '{TrainingPermissionsDataSet}' to be selected."
            );
        }

        foreach (var dataSetKey in selectedDataSets)
        {
            if (!DataSetCatalog.TryGetValue(dataSetKey, out var dataSet))
            {
                throw new InvalidOperationException(
                    $"Seed-data set '{dataSetKey}' is not registered. Available data sets: {string.Join(", ", DataSetCatalog.Keys.Order())}"
                );
            }

            if (!dataSet.IsAvailable(configuration))
            {
                throw new InvalidOperationException(
                    $"Seed-data set '{dataSetKey}' requires FeatureFlags:{dataSet.RequiredFeature} to be enabled."
                );
            }

            dataSet.Register(services);
        }

        services.AddSingleton(new ResolvedSeedDataConfiguration(selectedDataSets));

        return services;
    }

    private sealed record SeedDataSetDescriptor(
        string Key,
        Action<IServiceCollection> Register,
        string? RequiredFeature = null,
        Func<IConfiguration, bool>? AvailableWhen = null
    )
    {
        public bool IsAvailable(IConfiguration configuration) => AvailableWhen?.Invoke(configuration) ?? true;
    }
}
