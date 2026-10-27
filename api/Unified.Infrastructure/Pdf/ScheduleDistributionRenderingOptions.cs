namespace Unified.Infrastructure.Pdf;

public sealed class ScheduleDistributionRenderingOptions
{
    public const string SectionName = "ScheduleDistribution";
    public const int MinimumViewportWidth = 1280;
    public const int MaximumViewportWidth = 3840;
    public const int MinimumViewportHeight = 720;
    public const int MaximumViewportHeight = 2160;

    public string FrontendBaseUrl { get; set; } = string.Empty;

    public int DefaultViewportWidth { get; set; } = 1440;

    public int DefaultViewportHeight { get; set; } = 1000;

    public int RenderTimeoutSeconds { get; set; } = 45;

    public int MaximumConcurrentRenders { get; set; } = 2;

    public bool VerboseDiagnosticsEnabled { get; set; }
}
