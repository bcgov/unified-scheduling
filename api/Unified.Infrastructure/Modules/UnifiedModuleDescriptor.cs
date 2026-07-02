namespace Unified.Infrastructure.Modules;

public sealed record UnifiedModuleDescriptor(
    string Name,
    Func<FeatureFlags.FeatureFlags, bool> IsEnabled,
    IReadOnlyCollection<string> RequiredModuleNames
);
