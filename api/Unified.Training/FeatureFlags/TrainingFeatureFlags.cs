using System.ComponentModel.DataAnnotations;
using Unified.Common.FeatureFlags;

namespace Unified.Training.FeatureFlags;

/// <summary>
/// Feature flags specific to the Training module.
/// Binds from "FeatureFlags:Training" section in appsettings.json.
/// </summary>
public class TrainingFeatureFlags : IFeatureFlags
{
    public const string SourceName = "Training";
    public static string Section => IFeatureFlags.GetSection(SourceName);

    public string Source { get; } = SourceName;

    [Required(ErrorMessage = "Training.Enabled feature flag is required.")]
    public bool Enabled { get; set; }
}
