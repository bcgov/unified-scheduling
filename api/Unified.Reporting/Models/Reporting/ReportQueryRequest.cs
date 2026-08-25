namespace Unified.Reporting.Models.Reporting;

public sealed record ReportQueryRequest(
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> Filters,
    int Page = 1,
    int PageSize = 50,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Asc,
    string? TimeZone = null
);