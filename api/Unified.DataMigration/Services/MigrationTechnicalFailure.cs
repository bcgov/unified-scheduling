using System.Diagnostics;
using System.Text;
using Npgsql;
using Unified.Db;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

internal static class MigrationTechnicalFailure
{
    // Fits ErrorMessage (varchar 2048) after the fixed job-failure prefix.
    internal const int MaxDescriptionLength = 1800;

    // Exception chain (type + message), PostgreSQL SQLSTATE/constraint/table and the
    // innermost Unified.DataMigration frames. PostgresException.Detail is omitted
    // because it echoes row values.
    internal static string Describe(Exception ex)
    {
        var text = new StringBuilder();
        var depth = 0;
        for (var current = ex; current is not null && depth < 4; current = current.InnerException, depth++)
        {
            if (depth > 0)
                text.Append(" ---> ");
            // PostgresException.Message embeds Detail; MessageText does not.
            var message = current is PostgresException p ? p.MessageText : current.Message;
            text.Append(current.GetType().FullName).Append(": ").Append(Truncate(message, 300));
            if (current is PostgresException pg)
                text.Append(" [SqlState=")
                    .Append(pg.SqlState)
                    .Append(pg.ConstraintName is null ? "" : ";Constraint=" + pg.ConstraintName)
                    .Append(pg.TableName is null ? "" : ";Table=" + pg.TableName)
                    .Append(']');
        }
        var innermost = ex;
        while (innermost.InnerException is not null)
            innermost = innermost.InnerException;
        var frames = new[] { innermost, ex }
            .Distinct()
            .SelectMany(x => new StackTrace(x, true).GetFrames())
            .Where(x => x.GetMethod()?.DeclaringType?.Namespace?.StartsWith("Unified.DataMigration") == true)
            .Select(x => $"{x.GetMethod()!.DeclaringType!.Name}.{x.GetMethod()!.Name}:{x.GetFileLineNumber()}")
            .Distinct()
            .Take(8)
            .ToList();
        if (frames.Count > 0)
            text.Append(" | at ").AppendJoin(" < ", frames);
        return Truncate(text.ToString(), MaxDescriptionLength);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 3)] + "...";

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
