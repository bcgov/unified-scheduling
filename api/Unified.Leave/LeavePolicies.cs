using Unified.Authorization;

namespace Unified.Leave;

/// <summary>
/// Pre-built policy name constants for use in <c>[Authorize(Policy = ...)]</c> attributes
/// within the Leave module. Combines <see cref="AuthorizationModule.PolicyPrefix"/>
/// with each permission name so controllers never perform string concatenation.
/// </summary>
public static class LeavePolicies
{
    public const string LeaveView = AuthorizationModule.PolicyPrefix + nameof(Permissions.LeaveView);
    public const string LeaveCreateAndAssign =
        AuthorizationModule.PolicyPrefix + nameof(Permissions.LeaveCreateAndAssign);
    public const string LeaveEdit = AuthorizationModule.PolicyPrefix + nameof(Permissions.LeaveEdit);
    public const string LeaveDelete = AuthorizationModule.PolicyPrefix + nameof(Permissions.LeaveDelete);

    // Accept either the scheduling permission or the matching user (My Team) permission.
    public const string LeaveViewOrUser = AuthorizationModule.PolicyPrefix + "LeaveViewOrUser";
    public const string LeaveCreateOrUser = AuthorizationModule.PolicyPrefix + "LeaveCreateOrUser";
    public const string LeaveEditOrUser = AuthorizationModule.PolicyPrefix + "LeaveEditOrUser";
    public const string LeaveDeleteOrUser = AuthorizationModule.PolicyPrefix + "LeaveDeleteOrUser";

    // Leave types are a lookup used when recording leave, so either leave view permission grants access.
    public const string LeaveTypesView = AuthorizationModule.PolicyPrefix + "LeaveTypesView";
}
