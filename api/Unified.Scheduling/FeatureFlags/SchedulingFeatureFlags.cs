using System.ComponentModel.DataAnnotations;
using Unified.Common.FeatureFlags;

namespace Unified.Scheduling.FeatureFlags;

/// <summary>
/// Feature flags specific to the Scheduling module.
/// Binds from "FeatureFlags:Scheduling" in application configuration.
/// </summary>
public sealed class SchedulingFeatureFlags : IFeatureFlags
{
    public const string SourceName = "Scheduling";
    public static string Section => IFeatureFlags.GetSection(SourceName);

    public string Source { get; } = SourceName;

    [Required(ErrorMessage = "Scheduling.Enabled feature flag is required.")]
    public bool Enabled { get; set; }
}
