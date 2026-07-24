using System.ComponentModel.DataAnnotations;

namespace Unified.JCInterface.Options;

/// <summary>
/// All configuration for the JC Interface — including
/// the HTTP client credentials and every behaviour flag — bound from the
/// single "JCInterface" section in appsettings.json.
/// </summary>
public class JCInterfaceOptions
{
    public const string SectionName = "JCInterface";

    // ------------------------------------------------------------------
    // JC Interface HTTP client — authentication & connectivity
    // ------------------------------------------------------------------

    /// <summary>
    /// Base URL of the JC Interface Location Services API. Must be an absolute HTTPS
    /// URL — Basic Auth credentials are sent on every request, so plain HTTP would
    /// expose them in transit.
    /// Only required when <see cref="SkipSync"/> is false.
    /// </summary>
    [Required(ErrorMessage = "JCInterface Url is required")]
    [HttpsUrl(ErrorMessage = "JCInterface Url must be a valid HTTPS URL")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Basic Auth username for the JC Interface API.
    /// Only required when <see cref="SkipSync"/> is false.
    /// </summary>
    [Required(ErrorMessage = "JCInterface Username is required")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Basic Auth password for the JC Interface API.
    /// Store in a secret — never commit to source control.
    /// Only required when <see cref="SkipSync"/> is false.
    /// </summary>
    [Required(ErrorMessage = "JCInterface Password is required")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// HTTP request timeout for calls to the JC Interface. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

    // ------------------------------------------------------------------
    // Sync behaviour flags
    // ------------------------------------------------------------------

    /// <summary>
    /// When true the entire sync pipeline is skipped. Useful in environments
    /// without VPN access to the JC Interface (e.g. local dev). When true,
    /// Url/Username/Password are not required and are not validated at startup.
    /// </summary>
    public bool SkipSync { get; set; } = false;

    /// <summary>
    /// When true, regions absent from the JC Interface response have their
    /// ExpiryDate set to UtcNow.
    /// </summary>
    public bool ExpireRegions { get; set; }

    /// <summary>
    /// When true, locations absent from the JC Interface response have their
    /// ExpiryDate set to UtcNow. Defaults to false because some locations are
    /// seeded via migration and are never returned by the JC Interface.
    /// </summary>
    public bool ExpireLocations { get; set; } = false;

    /// <summary>
    /// When true, court rooms absent from the JC Interface response have their
    /// ExpiryDate set to UtcNow.
    /// </summary>
    public bool ExpireCourtRooms { get; set; } = true;

    /// <summary>
    /// When true, users with no HomeLocationId are automatically assigned to
    /// <see cref="DefaultUserLocationName"/> after the location sync completes.
    /// Defaults to false since this behaviour isn't part of the JC-Interface sync
    /// itself and should be opted into explicitly.
    /// </summary>
    public bool AssociateUsersWithNoLocationToVictoria { get; set; } = false;

    /// <summary>
    /// Name of the location assigned to users with no HomeLocationId when
    /// <see cref="AssociateUsersWithNoLocationToVictoria"/> is enabled.
    /// </summary>
    public string DefaultUserLocationName = "Victoria Law Courts";

    // ------------------------------------------------------------------
    // Lookup dictionaries
    // ------------------------------------------------------------------

    /// <summary>
    /// Maps IANA Timezone ID → comma-separated JUSTIN location codes.
    /// Any location whose JustinLocationCode matches one of the codes is
    /// assigned that timezone. Locations with no match default to
    /// "America/Vancouver".
    /// IMPORTANT: This default list can be overridden in appsettings per
    /// application/environment.
    /// IMPORTANT: These location codes are PROD values and may differ in
    /// DEV/TEST.
    /// Reference: https://wiki.justice.gov.bc.ca/wiki/spaces/CSA/pages/525730003/Locations+and+Regions
    /// </summary>
    public Dictionary<string, string> LocationTimeZones { get; set; } =
        new()
        {
            // Cranbrook, Fernie, Sparwood, Golden, Invermere
            ["America/Edmonton"] = "4711,SCCB,4731,4951,SCGD,GCC,4741,SCIN,4771",
            // Dawson Creek, Tumbler Ridge, Chetwynd, Fort St. John
            ["America/Dawson_Creek"] = "5731,SCDC,KPAC,5955,5721,SCFJ",
            // Creston
            ["America/Creston"] = "SCCS,4721",
        };
}
