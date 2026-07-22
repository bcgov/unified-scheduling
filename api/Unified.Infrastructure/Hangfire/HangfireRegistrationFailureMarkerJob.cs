using Hangfire;
using Microsoft.Extensions.Logging;

namespace Unified.Infrastructure.Hangfire;

/// <summary>
/// Creates an explicit failed job entry when recurring-job registration fails,
/// making startup registration problems visible in the Hangfire dashboard.
/// </summary>
public sealed class HangfireRegistrationFailureMarkerJob(ILogger<HangfireRegistrationFailureMarkerJob> logger)
{
    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public Task Execute(string jobType, string failureSummary)
    {
        logger.LogError(
            "Recurring job registration failed for {JobType}: {FailureSummary}",
            jobType,
            failureSummary
        );

        throw new InvalidOperationException(
            $"Recurring job registration failed for '{jobType}'. {failureSummary}"
        );
    }
}