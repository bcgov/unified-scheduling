using System.ComponentModel.DataAnnotations;

namespace Unified.FeatureFlags;

public partial class FeatureFlags
{
    [Required(ErrorMessage = "TrainingModule feature flag is required.")]
    public bool TrainingModule { get; set; }
}
