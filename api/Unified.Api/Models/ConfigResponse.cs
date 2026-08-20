using System.Text.Json.Serialization;

namespace Unified.Api.Models;

/// <summary>
/// Configuration response returned by /api/config endpoint.
/// Feature flags are organized by module source for type-safe frontend access.
/// </summary>
public class ConfigResponse
{
    /// <summary>
    /// Module feature flags keyed by source names.
    /// </summary>
    [JsonPropertyName("featureFlags")]
    public Dictionary<string, object> FeatureFlags { get; set; } = [];

    /// <summary>
    /// Support email for the application
    /// </summary>
    public string? SupportEmail { get; set; }

    /// <summary>
    /// Application display name
    /// </summary>
    public string? ApplicationName { get; set; }
}
