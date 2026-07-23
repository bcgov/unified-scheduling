using Hangfire.Server;

namespace Unified.Hangfire;

/// <summary>
/// Global Hangfire server filter that makes the currently-executing job's
/// <see cref="PerformContext"/> available via <see cref="HangfireConsoleContext"/> for the
/// duration of the job (including everything it awaits), then clears it afterwards.
///
/// Registered once for all jobs via <c>GlobalJobFilters.Filters.Add(new HangfireConsoleLoggingFilter())</c>
/// in <see cref="HangfireModule"/> - individual <c>IRecurringJob</c> implementations don't need
/// to do anything special to get their logs mirrored to the dashboard's Console tab.
/// </summary>
public sealed class HangfireConsoleLoggingFilter : IServerFilter
{
    public void OnPerforming(PerformingContext filterContext) => HangfireConsoleContext.Current = filterContext;

    public void OnPerformed(PerformedContext filterContext) => HangfireConsoleContext.Current = null;
}
