using Unified.Authorization;
using Unified.Common.Seeding;

namespace Unified.Training;

/// <summary>
/// Static permission seed data owned by the Training module.
/// </summary>
public static class TrainingPermissionSeedData
{
    private const string PermissionGroupTraining = "Training";

    public static PermissionSeedConfiguration Configuration { get; } =
        new()
        {
            Permissions =
            [
                new()
                {
                    Group = PermissionGroupTraining,
                    Id = nameof(Permissions.TrainingsView),
                    Description = "View training types",
                },
                new()
                {
                    Group = PermissionGroupTraining,
                    Id = nameof(Permissions.TrainingsCreate),
                    Description = "Create training types",
                },
                new()
                {
                    Group = PermissionGroupTraining,
                    Id = nameof(Permissions.TrainingsEdit),
                    Description = "Edit training types",
                },
                new()
                {
                    Group = PermissionGroupTraining,
                    Id = nameof(Permissions.TrainingsDelete),
                    Description = "Delete training types",
                },
                new()
                {
                    Group = PermissionGroupTraining,
                    Id = nameof(Permissions.TrainingsRecordsManageForOthers),
                    Description = "Create, update, and delete training records on behalf of other users",
                },
                new()
                {
                    Group = PermissionGroupTraining,
                    Id = nameof(Permissions.TrainingsEditPast),
                    Description = "Edit training records where the awarded date is in the past",
                },
                new()
                {
                    Group = PermissionGroupTraining,
                    Id = nameof(Permissions.TrainingsRemovePast),
                    Description = "Remove training records where the awarded date is in the past",
                },
                new()
                {
                    Group = PermissionGroupTraining,
                    Id = nameof(Permissions.TrainingsAdjustExpiry),
                    Description = "Manually override the expiry date on a training record",
                },
            ],
        };
}
