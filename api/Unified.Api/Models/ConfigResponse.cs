using System.Text.Json.Serialization;
using Unified.Calendar.FeatureFlags;
using Unified.Scheduling.FeatureFlags;
using Unified.Stats.FeatureFlags;
using Unified.Training.FeatureFlags;
using Unified.UserManagement.FeatureFlags;

namespace Unified.Api.Models;

/// <summary>
/// Configuration response returned by /api/config endpoint.
/// Feature flags are organized by module source for type-safe frontend access.
/// </summary>
public class ConfigResponse
{
    /// <summary>
    /// Module feature flags keyed by source names for strongly typed API contracts.
    /// </summary>
    [JsonPropertyName("featureFlags")]
    public FeatureFlagsResponse FeatureFlags { get; set; } = new();

    /// <summary>
    /// Support email for the application
    /// </summary>
    public string? SupportEmail { get; set; }

    /// <summary>
    /// Application display name
    /// </summary>
    public string? ApplicationName { get; set; }
}

/// <summary>
/// Strongly typed feature flags payload returned by /api/config.
/// JSON property names match module SourceName values.
/// </summary>
public class FeatureFlagsResponse
{
    [JsonPropertyName(UserManagementFeatureFlags.SourceName)]
    public UserManagementFeatureFlags? UserManagement { get; set; }

    [JsonPropertyName(CalendarFeatureFlags.SourceName)]
    public CalendarFeatureFlags? Calendar { get; set; }

    [JsonPropertyName(SchedulingFeatureFlags.SourceName)]
    public SchedulingFeatureFlags? Scheduling { get; set; }

    [JsonPropertyName(StatsFeatureFlags.SourceName)]
    public StatsFeatureFlags? Stats { get; set; }

    [JsonPropertyName(TrainingFeatureFlags.SourceName)]
    public TrainingFeatureFlags? Training { get; set; }
}
