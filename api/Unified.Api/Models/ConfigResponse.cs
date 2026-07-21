using Unified.FeatureFlags;

namespace Unified.Api.Models;

public class ConfigResponse
{
    public required FeatureFlags.FeatureFlags FeatureFlags { get; set; }

    public string? SupportEmail { get; set; }

    public string? ApplicationName { get; set; }
}
