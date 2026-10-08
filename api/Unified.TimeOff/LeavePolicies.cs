using Unified.Authorization;

namespace Unified.TimeOff;

/// <summary>
/// Pre-built policy name constants for use in <c>[Authorize(Policy = ...)]</c> attributes
/// within the Leave module.
/// </summary>
public static class LeavePolicies
{
    // Leave types are a lookup used when recording time off, so either time-off view permission grants access.
    public const string LeaveTypesView = AuthorizationModule.PolicyPrefix + "LeaveTypesView";
}
