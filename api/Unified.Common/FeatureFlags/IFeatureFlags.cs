namespace Unified.Common.FeatureFlags;

/// <summary>
/// Base contract for module-specific feature flags.
/// Each module implements this interface to register its own feature flags
/// and constraints with the dependency injection container.
/// </summary>
public interface IFeatureFlags
{
    /// <summary>
    /// Root configuration section for all feature flags.
    /// </summary>
    const string FeatureFlags = "FeatureFlags";

    /// <summary>
    /// Build a module-specific configuration section path.
    /// </summary>
    /// <param name="source">Module source name.</param>
    /// <returns>Section path (e.g., FeatureFlags:UserManagement).</returns>
    static string GetSection(string source) => FeatureFlags + ":" + source;

    /// <summary>
    /// Module-specific configuration section path.
    /// </summary>
    string Section => GetSection(Source);

    /// <summary>
    /// Module/source name (e.g., "UserManagement", "Calendar", "Stats")
    /// Used as dictionary key when aggregating all module flags.
    /// </summary>
    string Source { get; }

    /// <summary>
    /// Whether this module's features are globally enabled.
    /// Individual feature constraints (e.g., UserBadgeNumber.Required) are
    /// independent of this flag.
    /// </summary>
    bool Enabled { get; }
}
