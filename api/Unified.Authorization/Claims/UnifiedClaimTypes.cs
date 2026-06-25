namespace Unified.Authorization.Claims;

/// <summary>
/// Custom claim type constants added by <see cref="UnifiedClaimsTransformer"/>.
/// </summary>
public static class UnifiedClaimTypes
{
    private const string Prefix = nameof(UnifiedClaimTypes) + "/";

    /// <summary>
    /// Claim type for the user's Idir ID without the "@idir" suffix (e.g., "1234-5678-90ab-cdef").
    /// </summary>
    public const string IdirId = "keycloak/idir_user_guid";

    /// <summary>
    /// Claim type used for individual permission values (e.g., "ShiftsEdit").
    /// Multiple claims of this type may exist — one per granted permission.
    /// </summary>
    public const string Permission = Prefix + nameof(Permission);

    /// <summary>
    /// User id from DB
    /// </summary>
    public const string UserId = Prefix + nameof(UserId);

    /// <summary>
    /// The user's first name.
    /// </summary>
    public const string FirstName = Prefix + nameof(FirstName);

    /// <summary>
    /// The user's last name.
    /// </summary>
    public const string LastName = Prefix + nameof(LastName);

    /// <summary>
    /// The user's home location ID.
    /// </summary>
    public const string HomeLocationId = Prefix + nameof(HomeLocationId);

    /// <summary>
    /// The timezone for the user's home location.
    /// </summary>
    public const string HomeLocationTimezone = Prefix + nameof(HomeLocationTimezone);
}
