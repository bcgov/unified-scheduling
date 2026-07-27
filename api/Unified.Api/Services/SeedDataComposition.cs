using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Common.Seeding;

namespace Unified.Api.Services;

/// <summary>
/// Registers the seed-data contributions selected by deployment configuration.
/// Application configuration selects data sets; modules own executable seeder registration.
/// </summary>
public static class SeedDataComposition
{
    public static IServiceCollection AddConfiguredSeedData(
        this IServiceCollection services,
        IConfiguration configuration,
        IEnumerable<SeedDataSetDescriptor> dataSets
    )
    {
        var dataSetCatalog = dataSets.ToDictionary(dataSet => dataSet.Key, StringComparer.OrdinalIgnoreCase);
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

        foreach (
            var dataSet in dataSetCatalog.Values.Where(dataSet =>
                dataSet.RequiredFeature is not null && dataSet.IsAvailable(configuration)
            )
        )
        {
            if (!selectedDataSets.Contains(dataSet.Key, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"FeatureFlags:{dataSet.RequiredFeature} requires seed-data set '{dataSet.Key}' to be selected."
                );
            }
        }

        foreach (var dataSetKey in selectedDataSets)
        {
            if (!dataSetCatalog.TryGetValue(dataSetKey, out var dataSet))
            {
                throw new InvalidOperationException(
                    $"Seed-data set '{dataSetKey}' is not registered. Available data sets: {string.Join(", ", dataSetCatalog.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))}"
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
}
