using Hangfire;
using Unified.Common.Jobs;

namespace Unified.Hangfire;

/// <summary>
/// Registers or removes a single <see cref="IRecurringJob"/> with Hangfire based on its
/// current <see cref="IRecurringJob.CronSchedule"/>.
/// </summary>
public static class RecurringJobHelper
{
    private static readonly string[] DisabledSchedules = ["disable", "disabled"];

    public static void AddOrUpdate(IRecurringJob job)
    {
        if (
            !string.IsNullOrWhiteSpace(job.CronSchedule)
            && !DisabledSchedules.Any(e => e.Equals(job.CronSchedule, StringComparison.OrdinalIgnoreCase))
        )
        {
            var options = new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Vancouver"),
            };

            // Hangfire requires an argument at registration time; CancellationToken.None
            // acts as a placeholder and Hangfire provides the live execution token.
            RecurringJob.AddOrUpdate(
                job.JobName,
                () => job.Execute(null, CancellationToken.None),
                job.CronSchedule,
                options
            );
        }
        else
        {
            // Job is disabled. Remove it if it was previously registered.
            RecurringJob.RemoveIfExists(job.JobName);
        }
    }
}
