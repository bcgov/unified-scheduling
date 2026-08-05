namespace Unified.FeatureFlags;

/// <summary>
/// Base contract for module-specific feature flags.
/// Each module implements this interface to register its own feature flags
/// and constraints with the dependency injection container.
/// </summary>
public interface IFeatureFlags
{
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
