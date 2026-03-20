namespace Unified.FeatureFlags;

/// <summary>
/// Feature flags configuration with validation.
/// Binds from "FeatureFlags" section in appsettings.json.
/// Provides compile-time safety with validation on startup.
/// </summary>
public partial class FeatureFlags
{
    public const string SectionName = "FeatureFlags";
}
