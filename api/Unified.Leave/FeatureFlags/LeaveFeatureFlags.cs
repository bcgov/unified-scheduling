using System.ComponentModel.DataAnnotations;
using Unified.Common.FeatureFlags;

namespace Unified.Leave.FeatureFlags;

/// <summary>
/// Feature flags specific to the Leave module.
/// Binds from "FeatureFlags:Leave" section in appsettings.json.
/// </summary>
public sealed class LeaveFeatureFlags : IFeatureFlags
{
    public const string SourceName = "Leave";
    public static string Section => IFeatureFlags.GetSection(SourceName);

    public string Source { get; } = SourceName;

    [Required(ErrorMessage = "Leave.Enabled feature flag is required.")]
    public bool Enabled { get; set; }
}
