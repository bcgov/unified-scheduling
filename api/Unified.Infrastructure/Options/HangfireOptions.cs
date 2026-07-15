using Hangfire;

namespace Unified.Infrastructure.Options;

/// <summary>
/// Configuration for Hangfire background job processing, bound from the
/// "Hangfire" section in appsettings.json.
/// </summary>
public class HangfireOptions
{
    public const string SectionName = "Hangfire";

    /// <summary>
    /// Number of automatic retry attempts applied globally to every recurring job.
    /// Defaults to Hangfire's built-in default retry count.
    /// </summary>
    public int RetryCount { get; set; } = AutomaticRetryAttribute.DefaultRetryAttempts;
}
