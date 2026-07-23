using Hangfire.Server;

namespace Unified.Hangfire;

/// <summary>
/// Holds the currently-executing job's <see cref="PerformContext"/> as an ambient,
/// async-flow-scoped value. Set by <see cref="HangfireConsoleLoggingFilter"/> around every
/// job execution and read by <see cref="HangfireConsoleLoggerProvider"/> so that ordinary
/// <c>ILogger</c> calls made anywhere in the job's call graph are mirrored to the job's
/// Console tab in the Hangfire dashboard.
/// </summary>
internal static class HangfireConsoleContext
{
    private static readonly AsyncLocal<PerformContext?> AmbientContext = new();

    public static PerformContext? Current
    {
        get => AmbientContext.Value;
        set => AmbientContext.Value = value;
    }
}
