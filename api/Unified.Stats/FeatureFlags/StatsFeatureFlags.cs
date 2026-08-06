using System.ComponentModel.DataAnnotations;
using Unified.Common.FeatureFlags;

namespace Unified.Stats.FeatureFlags;

/// <summary>
/// Feature flags specific to the Stats module.
/// Binds from "FeatureFlags:Stats" section in appsettings.json.
/// </summary>
public class StatsFeatureFlags : IFeatureFlags
{
    public const string SourceName = "Stats";
    public static string Section => IFeatureFlags.GetSection(SourceName);

    public string Source { get; } = SourceName;

    [Required(ErrorMessage = "Stats.Enabled feature flag is required.")]
    public bool Enabled { get; set; }
}
