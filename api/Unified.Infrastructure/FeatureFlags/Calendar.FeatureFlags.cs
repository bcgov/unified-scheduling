using System.ComponentModel.DataAnnotations;

namespace Unified.FeatureFlags;

public partial class FeatureFlags
{
    [Required(ErrorMessage = "CalendarModule feature flag is required.")]
    public bool CalendarModule { get; set; }
}
