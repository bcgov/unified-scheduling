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
    public const string ShiftsImport = AuthorizationModule.PolicyPrefix + nameof(Permissions.ShiftsImport);
    public const string ShiftsViewAllFuture =
        AuthorizationModule.PolicyPrefix + nameof(Permissions.ShiftsViewAllFuture);
}
