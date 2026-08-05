using System.ComponentModel.DataAnnotations;
using Unified.Common.FeatureFlags;

namespace Unified.UserManagement.FeatureFlags;

/// <summary>
/// Feature flags specific to the UserManagement module.
/// Binds from "FeatureFlags:UserManagement" section in appsettings.json.
/// </summary>
public class UserManagementFeatureFlags : IFeatureFlags
{
    public string Source => "UserManagement";

    [Required(ErrorMessage = "UserManagement.Enabled feature flag is required.")]
    public bool Enabled { get; set; }

    public UserBadgeNumberFlags UserBadgeNumber { get; set; } = new();
}

/// <summary>
/// UserBadgeNumber-specific constraints.
/// Allows per-feature configuration beyond simple enabled/disabled.
/// </summary>
public class UserBadgeNumberFlags
{
    [Required(ErrorMessage = "UserBadgeNumber.Enabled is required.")]
    public bool Enabled { get; set; }

    [Required(ErrorMessage = "UserBadgeNumber.Required is required.")]
    public bool Required { get; set; }
}
