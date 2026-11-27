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

    // Exception types, PostgreSQL SQLSTATE/constraint/table and migration frames only.
    // Exception messages (including PostgreSQL MessageText, Detail and Hint) can contain
    // source values or credentials and must never reach ordinary logs or failure evidence.
    internal static string Describe(Exception ex)
    {
        var text = new StringBuilder();
        var depth = 0;
        for (var current = ex; current is not null && depth < 4; current = current.InnerException, depth++)
        {
            if (depth > 0)
                text.Append(" ---> ");
            text.Append(Truncate(current.GetType().FullName ?? current.GetType().Name, 200));
            if (current is PostgresException pg)
            {
                text.Append(" [SqlState=").Append(SafeSqlState(pg.SqlState) ? pg.SqlState : "invalid");
                if (SafeIdentifier(pg.ConstraintName))
                    text.Append(";Constraint=").Append(pg.ConstraintName);
                if (SafeIdentifier(pg.TableName))
                    text.Append(";Table=").Append(pg.TableName);
                text.Append(']');
            }
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

    internal static bool SafeSqlState(string? value) =>
        value is { Length: 5 } && value.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9');

    internal static bool SafeIdentifier(string? value) =>
        value is { Length: > 0 and <= 128 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '$' or '-');

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
