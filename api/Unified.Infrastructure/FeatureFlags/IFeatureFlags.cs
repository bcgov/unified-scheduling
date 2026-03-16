namespace Unified.FeatureFlags;

public interface IFeatureFlags
{
    FeatureFlags Current { get; }
}
