using System.ComponentModel.DataAnnotations;

namespace Unified.Infrastructure.Options;

/// <summary>
/// Keycloak configuration options
/// </summary>
public class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    /// <summary>
    /// Keycloak authority URL (e.g., https://keycloak.example.com/realms/myrealm)
    /// </summary>
    [Required(ErrorMessage = "Keycloak Authority is required")]
    [Url(ErrorMessage = "Keycloak Authority must be a valid URL")]
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Client ID for OpenID Connect
    /// </summary>
    [Required(ErrorMessage = "Keycloak Client ID is required")]
    public string Client { get; set; } = string.Empty;

    /// <summary>
    /// Client secret for OpenID Connect
    /// </summary>
    [Required(ErrorMessage = "Keycloak Client Secret is required")]
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Audience claim for JWT bearer token validation
    /// </summary>
    [Required(ErrorMessage = "Keycloak Audience is required")]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Time before access-token expiration when refresh should be attempted.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00.0000001", "10675199.02:48:05.4775807", ErrorMessage = "TokenRefreshThreshold must be greater than zero")]
    public TimeSpan TokenRefreshThreshold { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Identity provider hint
    /// </summary>
    public string? IdpHint { get; set; } = "idir";

    /// Base URL for redirect URIs (e.g., http://localhost:5000) - used in Docker/proxy scenarios
    /// </summary>
    /// <summary>
    public string? RedirectBaseUrl { get; set; } = "/";

    /// <summary>
    /// OIDC callback path
    /// </summary>
    public string CallbackPath { get; set; } = "/api/auth/signin-oidc";

    /// <summary>
    /// Refresh token endpoint (optional, defaults to {Authority}/protocol/openid-connect/token)
    /// </summary>
    public string? RefreshTokenEndpoint { get; set; } = "/protocol/openid-connect/token";

    /// <summary>
    /// Logout endpoint (optional, defaults to {Authority}/protocol/openid-connect/logout)
    /// </summary>
    public string? LogoutEndpoint { get; set; } = "/protocol/openid-connect/logout";

    /// <summary>
    /// Keycloak identity provider hint for automatic provider selection.
    /// </summary>
    public string KcIdpHint { get; set; } = "idir";
    public string CookieName { get; set; } = "UnifiedAuth";
}
