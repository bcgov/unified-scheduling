using Unified.Infrastructure.Options;

namespace Unified.Api.Models;

public class ConfigResponse
{
    public required FeatureFlagsOptions FeatureFlags { get; set; }
}
