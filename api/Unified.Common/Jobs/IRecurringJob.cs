using Hangfire.Server;

namespace Unified.Common.Jobs;

/// <summary>
/// Contract for a Hangfire recurring job. Implementations are discovered via DI
/// (<c>IEnumerable&lt;IRecurringJob&gt;</c>) across all modules and registered/updated on
/// startup by <c>Unified.Infrastructure.Hangfire.HangfireJobRegistrationService</c>.
///
/// Lives in <c>Unified.Common</c> (rather than <c>Unified.Infrastructure</c>) because domain
/// modules implementing recurring jobs (e.g. <c>Unified.Api.Jobs.JCSyncRecurringJob</c>) need to
/// reference this contract without depending on Hangfire's server/dashboard wiring.
/// </summary>
public interface IRecurringJob
{
    /// <summary>
    /// Stable, unique identifier used as the Hangfire recurring job id.
    /// </summary>
    string JobName { get; }

    /// <summary>
    /// Cron expression controlling the job's schedule. Set to an empty string or
    /// "disabled" to remove the recurring job instead of scheduling it.
    /// </summary>
    string CronSchedule { get; }

    /// <summary>
    /// The work performed each time the job runs. Hangfire automatically supplies the real
    /// <see cref="PerformContext"/> at execution time (the value passed when scheduling is
    /// ignored). Ordinary <c>ILogger</c> calls made during this method (including in nested
    /// service calls) are automatically mirrored to the job's Console tab in the dashboard by
    /// the global <c>HangfireConsoleLoggingFilter</c> + <c>HangfireConsoleLoggerProvider</c> -
    /// implementations don't need to use <paramref name="context"/> directly unless they want
    /// custom formatting (e.g. <c>context.WriteLine(ConsoleTextColor, ...)</c>).
    /// </summary>
    Task Execute(PerformContext? context);
}
