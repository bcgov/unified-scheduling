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

    /// <summary>
    /// Claim type for the user's Idir ID without the "@idir" suffix (e.g., "1234-5678-90ab-cdef").
    /// </summary>
    public const string IdirId = "idir_user_guid";
}
