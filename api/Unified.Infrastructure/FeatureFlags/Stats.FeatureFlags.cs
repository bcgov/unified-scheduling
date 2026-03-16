using System.ComponentModel.DataAnnotations;

namespace Unified.FeatureFlags;

public partial class FeatureFlags
{
    [Required(ErrorMessage = "StatsModule feature flag is required.")]
    public bool StatsModule { get; set; }
}
