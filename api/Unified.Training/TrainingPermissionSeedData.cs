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
                    Id = nameof(Permissions.UserTrainingsView),
                    Description = "View user training records",
                },
                new()
                {
                    Group = PermissionGroupTraining,
                    Id = nameof(Permissions.UserTrainingsCreate),
                    Description = "Create user training records",
                },
                new()
                {
                    Group = PermissionGroupTraining,
                    Id = nameof(Permissions.UserTrainingsEdit),
                    Description = "Edit user training records",
                },
                new()
                {
                    Group = PermissionGroupTraining,
                    Id = nameof(Permissions.UserTrainingsDelete),
                    Description = "Delete user training records",
                },
            ],
        };
}
