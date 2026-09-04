using System.ComponentModel.DataAnnotations;
using Unified.Common.FeatureFlags;

namespace Unified.Reporting.FeatureFlags;

/// <summary>
/// Feature flags specific to the Reporting module.
/// Binds from "FeatureFlags:Reporting" section in appsettings.json.
/// </summary>
public sealed class ReportingFeatureFlags : IFeatureFlags
{
    public const string SourceName = "Reporting";
    public static string Section => IFeatureFlags.GetSection(SourceName);

    public string Source { get; } = SourceName;

    [Required(ErrorMessage = "Reporting.Enabled feature flag is required.")]
    public bool Enabled { get; set; }
}