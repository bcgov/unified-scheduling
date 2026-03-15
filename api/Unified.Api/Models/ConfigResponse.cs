using Unified.Flags;

namespace Unified.Api.Models;

public class ConfigResponse
{
    public required FeatureFlagsOptions FeatureFlags { get; set; }
}
