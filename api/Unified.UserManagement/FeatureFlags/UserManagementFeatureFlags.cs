using System.ComponentModel.DataAnnotations;
using Unified.Common.FeatureFlags;

namespace Unified.UserManagement.FeatureFlags;

/// <summary>
/// Feature flags specific to the UserManagement module.
/// Binds from "FeatureFlags:UserManagement" section in appsettings.json.
/// </summary>
public class UserManagementFeatureFlags : IFeatureFlags
{
    public const string SourceName = "UserManagement";
    public static string Section => IFeatureFlags.GetSection(SourceName);

    public string Source { get; } = SourceName;

    [Required(ErrorMessage = "UserManagement.Enabled feature flag is required.")]
    public bool Enabled { get; set; }

    public UserBadgeNumberFlags UserBadgeNumber { get; set; } = new();

    public LeaveFeatureFlags Leave { get; set; } = new();
}

/// <summary>
/// UserBadgeNumber-specific constraints.
/// Allows per-feature configuration beyond simple enabled/disabled.
/// </summary>
public class UserBadgeNumberFlags
{
    [Required(ErrorMessage = "UserBadgeNumber.Enabled is required.")]
    public bool Enabled { get; set; }
}

/// <summary>
/// Leave-specific feature flag. The leave calendar contribution requires both this and the
/// Calendar module flag to be enabled.
/// </summary>
public class LeaveFeatureFlags
{
    [Required(ErrorMessage = "Leave.Enabled feature flag is required.")]
    public bool Enabled { get; set; }
}
