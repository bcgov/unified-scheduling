using Microsoft.Extensions.Options;

namespace Unified.Infrastructure.Options;

public sealed class FeatureFlagsAccessor(IOptionsMonitor<FeatureFlagsOptions> monitor) : IFeatureFlags
{
    public FeatureFlagsOptions Current => monitor.CurrentValue;
}
