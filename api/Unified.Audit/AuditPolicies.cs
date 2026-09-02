using Unified.Authorization;

namespace Unified.Audit;

/// <summary>
/// Pre-built policy name constants for use in <c>[Authorize(Policy = ...)]</c> attributes
/// within the Audit module.
/// </summary>
public static class AuditPolicies
{
    public const string AuditRead = AuthorizationModule.PolicyPrefix + nameof(Permissions.AuditRead);
}
