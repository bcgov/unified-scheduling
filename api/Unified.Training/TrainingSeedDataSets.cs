using Microsoft.Extensions.Configuration;
using Unified.Authorization.Seeders;
using Unified.Common.Seeding;

namespace Unified.Training;

public static class TrainingSeedDataSets
{
    public const string TrainingPermissionsDataSet = nameof(TrainingPermissionsDataSet);

    public static IReadOnlyList<SeedDataSetDescriptor> All { get; } =
    [
        new(
            TrainingPermissionsDataSet,
            [
                new PermissionSeedConfiguration
                {
                    Source = TrainingPermissionsDataSet,
                    Definitions = TrainingPermissionSeedData.Instance.Definitions,
                },
            ],
            RequiredFeature: "TrainingModule",
            AvailableWhen: configuration => configuration.GetValue<bool>("FeatureFlags:TrainingModule")
        ),
    ];
}