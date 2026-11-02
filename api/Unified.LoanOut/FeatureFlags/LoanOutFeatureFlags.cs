using System.ComponentModel.DataAnnotations;
using Unified.Common.FeatureFlags;

namespace Unified.LoanOut.FeatureFlags;

/// <summary>
/// Feature flags specific to the LoanOut module.
/// Binds from "FeatureFlags:LoanOut" section in appsettings.json.
/// </summary>
public sealed class LoanOutFeatureFlags : IFeatureFlags
{
    public const string SourceName = "LoanOut";
    public static string Section => IFeatureFlags.GetSection(SourceName);

    public string Source { get; } = SourceName;

    [Required(ErrorMessage = "LoanOut.Enabled feature flag is required.")]
    public bool Enabled { get; set; }
}
