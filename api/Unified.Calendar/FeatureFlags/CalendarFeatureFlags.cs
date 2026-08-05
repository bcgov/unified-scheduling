using System.ComponentModel.DataAnnotations;
using Unified.Common.FeatureFlags;

namespace Unified.Calendar.FeatureFlags;

/// <summary>
/// Feature flags specific to the Calendar module.
/// Binds from "FeatureFlags:Calendar" section in appsettings.json.
/// </summary>
public class CalendarFeatureFlags : IFeatureFlags
{
    public string Source => "Calendar";

    [Required(ErrorMessage = "Calendar.Enabled feature flag is required.")]
    public bool Enabled { get; set; }
}
