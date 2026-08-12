using Unified.Authorization;

namespace Unified.Training;

/// <summary>
/// Pre-built policy name constants for use in <c>[Authorize(Policy = ...)]</c> attributes
/// within the Training module. Combines <see cref="AuthorizationModule.PolicyPrefix"/>
/// with each permission name so controllers never perform string concatenation.
/// </summary>
public static class TrainingPolicies
{
    public const string TrainingsView = AuthorizationModule.PolicyPrefix + nameof(Permissions.TrainingsView);

    public const string TrainingsCreate = AuthorizationModule.PolicyPrefix + nameof(Permissions.TrainingsCreate);

    public const string TrainingsEdit = AuthorizationModule.PolicyPrefix + nameof(Permissions.TrainingsEdit);

    public const string UserTrainingsView = AuthorizationModule.PolicyPrefix + nameof(Permissions.UserTrainingsView);

    public const string UserTrainingsCreate =
        AuthorizationModule.PolicyPrefix + nameof(Permissions.UserTrainingsCreate);

    public const string UserTrainingsEdit = AuthorizationModule.PolicyPrefix + nameof(Permissions.UserTrainingsEdit);

    public const string UserTrainingsDelete =
        AuthorizationModule.PolicyPrefix + nameof(Permissions.UserTrainingsDelete);
}
