using Microsoft.Extensions.Options;

namespace Unified.FeatureFlags;

public sealed class FeatureFlagsAccessor(IOptionsMonitor<FeatureFlags> monitor) : IFeatureFlags
{
    public FeatureFlags Current => monitor.CurrentValue;
}
