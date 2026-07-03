using Unified.Authorization;

namespace Unified.Training;

/// <summary>
/// Pre-built policy name constants for use in <c>[Authorize(Policy = ...)]</c> attributes
/// within the Training module. Combines <see cref="AuthorizationModule.PolicyPrefix"/>
/// with each permission name so controllers never perform string concatenation.
/// </summary>
public static class TrainingPolicies
{
    public const string TrainingsView =
        AuthorizationModule.PolicyPrefix + nameof(Permissions.TrainingsView);

    public const string TrainingsCreate =
        AuthorizationModule.PolicyPrefix + nameof(Permissions.TrainingsCreate);

    public const string TrainingsEdit =
        AuthorizationModule.PolicyPrefix + nameof(Permissions.TrainingsEdit);

    public const string TrainingsDelete =
        AuthorizationModule.PolicyPrefix + nameof(Permissions.TrainingsDelete);

    public const string TrainingRecordsManageForOthers =
        AuthorizationModule.PolicyPrefix + nameof(Permissions.TrainingRecordsManageForOthers);

    public const string TrainingEditPast =
        AuthorizationModule.PolicyPrefix + nameof(Permissions.TrainingEditPast);

    public const string TrainingRemovePast =
        AuthorizationModule.PolicyPrefix + nameof(Permissions.TrainingRemovePast);

    public const string TrainingAdjustExpiry =
        AuthorizationModule.PolicyPrefix + nameof(Permissions.TrainingAdjustExpiry);
}
