using System.ComponentModel.DataAnnotations;
using Unified.Common.FeatureFlags;

namespace Unified.TimeOff.FeatureFlags;

/// <summary>
/// Feature flags specific to the TimeOff module.
/// Binds from "FeatureFlags:TimeOff" section in appsettings.json.
/// </summary>
public sealed class TimeOffFeatureFlags : IFeatureFlags
{
    public const string SourceName = "TimeOff";
    public static string Section => IFeatureFlags.GetSection(SourceName);

    public string Source { get; } = SourceName;

    [Required(ErrorMessage = "TimeOff.Enabled feature flag is required.")]
    public bool Enabled { get; set; }
}
