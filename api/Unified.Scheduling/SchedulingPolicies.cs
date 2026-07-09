using Unified.Authorization;

namespace Unified.Scheduling;

/// <summary>
/// Pre-built policy name constants for use in <c>[Authorize(Policy = ...)]</c> attributes
/// within the Scheduling module. Combines <see cref="AuthorizationModule.PolicyPrefix"/>
/// with each permission name so controllers never perform string concatenation.
/// </summary>
public static class SchedulingPolicies
{
    public const string ShiftsView = AuthorizationModule.PolicyPrefix + nameof(Permissions.ShiftsView);
    public const string ShiftsCreateAndAssign =
        AuthorizationModule.PolicyPrefix + nameof(Permissions.ShiftsCreateAndAssign);
    public const string ShiftsEdit = AuthorizationModule.PolicyPrefix + nameof(Permissions.ShiftsEdit);
    public const string ShiftsExpire = AuthorizationModule.PolicyPrefix + nameof(Permissions.ShiftsExpire);
    public const string AssignmentsView = AuthorizationModule.PolicyPrefix + nameof(Permissions.AssignmentsView);
    public const string AssignmentsCreate = AuthorizationModule.PolicyPrefix + nameof(Permissions.AssignmentsCreate);
    public const string AssignmentsAssign = AuthorizationModule.PolicyPrefix + nameof(Permissions.AssignmentsAssign);
    public const string AssignmentsEdit = AuthorizationModule.PolicyPrefix + nameof(Permissions.AssignmentsEdit);
    public const string AssignmentsExpire = AuthorizationModule.PolicyPrefix + nameof(Permissions.AssignmentsExpire);
    public const string AssignmentTypeRead = AuthorizationModule.PolicyPrefix + nameof(Permissions.AssignmentTypeRead);
    public const string AssignmentTypeWrite = AuthorizationModule.PolicyPrefix + nameof(Permissions.AssignmentTypeWrite);
    public const string AssignmentTypeExpire = AuthorizationModule.PolicyPrefix + nameof(Permissions.AssignmentTypeExpire);
}
