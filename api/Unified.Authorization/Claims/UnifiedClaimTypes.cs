namespace Unified.Authorization.Claims;

/// <summary>
/// Custom claim type constants added by <see cref="PermissionClaimsTransformer"/>.
/// </summary>
public static class UnifiedClaimTypes
{
    /// <summary>
    /// Claim type used for individual permission values (e.g., "ShiftsEdit").
    /// Multiple claims of this type may exist — one per granted permission.
    /// </summary>
    public const string Permission = "unified/permission";
}
