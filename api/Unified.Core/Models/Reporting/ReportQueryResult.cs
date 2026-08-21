namespace Unified.Core.Models.Reporting;

public sealed record ReportQueryResult(
    string ReportKey,
    IReadOnlyCollection<ReportColumn> Columns,
    IReadOnlyCollection<IReadOnlyDictionary<string, object?>> Rows,
    int Page,
    int PageSize,
    int TotalRows,
    long ExecutionMs,
    IReadOnlyCollection<string>? Warnings = null
);
