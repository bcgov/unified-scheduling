using Unified.Db;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

internal static class MigrationTechnicalFailure
{
    internal static bool IsReportDiagnostic(string? errorType) =>
        errorType is "DispatchFailed" or "QueuedExecutionRefused" or "ExecutionFailed" or "Canceled";

    internal static void Record(UnifiedDbContext db, DataMigrationRun run, string errorType, string message)
    {
        if (run.Job == "Report")
            run.Status = run.FailedCount + run.RejectedCount > 0 ? "Failed" : "NeedsAction";
        else
        {
            run.Status = "Failed";
            run.FailedCount++;
        }
        run.CompletedOn = DateTimeOffset.UtcNow;
        db.DataMigrationFailures.Add(
            new DataMigrationFailure
            {
                Source = run.Source,
                RunId = run.Id,
                ErrorType = errorType,
                ErrorMessage = message,
                OccurredOn = DateTimeOffset.UtcNow,
            }
        );
    }
}
