using Unified.Authorization;

namespace Unified.Stats;

/// <summary>
/// Pre-built policy name constants for use in <c>[Authorize(Policy = ...)]</c> attributes
/// within the Stats module. Combines <see cref="AuthorizationModule.PolicyPrefix"/>
/// with each permission name so controllers never perform string concatenation.
/// </summary>
public static class StatsPolicies
{
    // --- Stats ---
    public const string StatsRecordsEnterForOthers =
        AuthorizationModule.PolicyPrefix + nameof(Permissions.StatsRecordsEnterForOthers);
}
