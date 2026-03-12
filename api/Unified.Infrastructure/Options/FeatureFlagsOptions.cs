using System.ComponentModel.DataAnnotations;

namespace Unified.Infrastructure.Options;

/// <summary>
/// Feature flags configuration with validation.
/// Binds from "FeatureFlags" section in appsettings.json.
/// Provides compile-time safety with validation on startup.
/// </summary>
public class FeatureFlagsOptions
{
    public const string SectionName = "FeatureFlags";

    [Required(ErrorMessage = "StatsModule feature flag is required.")]
    public bool StatsModule { get; set; }

    [Required(ErrorMessage = "UserBadgeNumber feature flag is required.")]
    public bool UserBadgeNumber { get; set; }
}
