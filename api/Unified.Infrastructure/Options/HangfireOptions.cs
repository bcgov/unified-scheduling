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
    /// Enables Hangfire service registration (storage, recurring-job discovery, and
    /// job registration hosted service).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Enables the Hangfire background processing server.
    /// </summary>
    public bool ServerEnabled { get; set; } = true;

    /// <summary>
    /// Enables the Hangfire dashboard endpoint mapping.
    /// </summary>
    public bool DashboardEnabled { get; set; } = true;

    /// <summary>
    /// When true, Hangfire attempts to create/upgrade its schema at runtime.
    /// Keep false in deployed environments that use least-privilege API credentials.
    /// </summary>
    public bool PrepareSchemaIfNecessary { get; set; } = false;

    /// <summary>
    /// Number of automatic retry attempts applied globally to every recurring job.
    /// Defaults to Hangfire's built-in default retry count.
    /// </summary>
    public int RetryCount { get; set; } = AutomaticRetryAttribute.DefaultRetryAttempts;
}
