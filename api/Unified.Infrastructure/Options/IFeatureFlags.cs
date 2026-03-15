namespace Unified.Infrastructure.Options;

public interface IFeatureFlags
{
    FeatureFlagsOptions Current { get; }
}
