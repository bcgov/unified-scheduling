namespace Unified.Reporting.Models.Reporting;

public sealed record ReportQueryResult(
    string ReportKey,
    IReadOnlyCollection<ReportColumn> Columns,
    IReadOnlyCollection<IReadOnlyDictionary<string, object?>> Rows,
    int Page,
    int PageSize,
    int TotalRows
);