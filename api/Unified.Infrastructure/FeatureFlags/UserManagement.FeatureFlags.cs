using System.ComponentModel.DataAnnotations;

namespace Unified.FeatureFlags;

public partial class FeatureFlags
{
    [Required(ErrorMessage = "MyTeamsModule feature flag is required.")]
    public bool MyTeamsModule { get; set; }

    [Required(ErrorMessage = "UserBadgeNumber feature flag is required.")]
    public bool UserBadgeNumber { get; set; }
}