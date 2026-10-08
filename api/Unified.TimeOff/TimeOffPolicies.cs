using Unified.Authorization;

namespace Unified.TimeOff;

/// <summary>
/// Pre-built policy name constants for use in <c>[Authorize(Policy = ...)]</c> attributes
/// within the TimeOff module. Combines <see cref="AuthorizationModule.PolicyPrefix"/>
/// with each permission name so controllers never perform string concatenation.
/// </summary>
public static class TimeOffPolicies
{
    public const string TimeOffView = AuthorizationModule.PolicyPrefix + nameof(Permissions.TimeOffView);
    public const string TimeOffCreateAndAssign =
        AuthorizationModule.PolicyPrefix + nameof(Permissions.TimeOffCreateAndAssign);
    public const string TimeOffEdit = AuthorizationModule.PolicyPrefix + nameof(Permissions.TimeOffEdit);
    public const string TimeOffDelete = AuthorizationModule.PolicyPrefix + nameof(Permissions.TimeOffDelete);

    // Accept either the scheduling permission or the matching user (My Team) permission.
    public const string TimeOffViewOrUser = AuthorizationModule.PolicyPrefix + "TimeOffViewOrUser";
    public const string TimeOffCreateOrUser = AuthorizationModule.PolicyPrefix + "TimeOffCreateOrUser";
    public const string TimeOffEditOrUser = AuthorizationModule.PolicyPrefix + "TimeOffEditOrUser";
    public const string TimeOffDeleteOrUser = AuthorizationModule.PolicyPrefix + "TimeOffDeleteOrUser";
}
