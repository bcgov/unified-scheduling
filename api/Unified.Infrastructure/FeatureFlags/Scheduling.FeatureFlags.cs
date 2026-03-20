using System.ComponentModel.DataAnnotations;

namespace Unified.FeatureFlags;

public partial class FeatureFlags
{
    [Required(ErrorMessage = "SchedulingModule feature flag is required.")]
    public bool SchedulingModule { get; set; }
}
