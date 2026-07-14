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
    /// Base URL of the JC Interface Location Services API.
    /// Must end with a trailing slash (e.g. "https://jc-interface.example.com/api/v1/").
    /// </summary>
    [Required(ErrorMessage = "JCInterface Url is required")]
    [Url(ErrorMessage = "JCInterface Url must be a valid URL")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Basic Auth username for the JC Interface API.
    /// </summary>
    [Required(ErrorMessage = "JCInterface Username is required")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Basic Auth password for the JC Interface API.
    /// Store in a secret — never commit to source control.
    /// </summary>
    [Required(ErrorMessage = "JCInterface Password is required")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// HTTP request timeout for calls to the JC Interface. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

    // ------------------------------------------------------------------
    // Sync timing
    // ------------------------------------------------------------------

    /// <summary>
    /// How often the background timer fires to check whether a sync is due.
    /// Defaults to 1 day.
    /// </summary>
    [Required]
    public TimeSpan CheckForUpdate { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Minimum time between actual sync runs. The last-sync timestamp is
    /// persisted in the JcSynchronization DB row and compared to UtcNow.
    /// Defaults to 23 hours 55 minutes.
    /// </summary>
    [Required]
    public TimeSpan UpdateEvery { get; set; } = TimeSpan.FromMinutes(23 * 60 + 55);

    // ------------------------------------------------------------------
    // Sync behaviour flags
    // ------------------------------------------------------------------

    /// <summary>
    /// When true the entire sync pipeline is skipped. Useful in environments
    /// without VPN access to the JC Interface (e.g. local dev).
    /// </summary>
    public bool SkipLocationUpdates { get; set; } = false;

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
    /// Victoria Law Courts after the location sync completes.
    /// </summary>
    public bool AssociateUsersWithNoLocationToVictoria { get; set; } = true;

    // ------------------------------------------------------------------
    // Lookup dictionaries
    // ------------------------------------------------------------------

    /// <summary>
    /// Maps Location Name → Region Name for locations that were seeded via
    /// migration and are never returned by the JC Interface. These are linked
    /// to their region manually during SyncLocations.
    /// </summary>
    public Dictionary<string, string> NonJCInterfaceLocationRegions { get; set; } = [];

    /// <summary>
    /// Maps IANA Timezone ID → comma-separated partial location name tokens.
    /// Any location whose name contains one of the tokens is assigned that
    /// timezone. Locations with no match default to "America/Vancouver".
    /// </summary>
    public Dictionary<string, string> LocationTimeZones { get; set; } = [];
}
