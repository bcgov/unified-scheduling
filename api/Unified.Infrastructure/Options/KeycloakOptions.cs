using System.ComponentModel.DataAnnotations;

namespace Unified.Infrastructure.Options;

/// <summary>
/// Keycloak configuration options
/// </summary>
public class KeycloakOptions
{
    public const string ConfigurationSection = "Keycloak";

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
    /// Token refresh threshold (e.g., "00:05:00" for 5 minutes)
    /// </summary>
    public TimeSpan TokenRefreshThreshold { get; set; } = TimeSpan.FromMinutes(3);

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

}
