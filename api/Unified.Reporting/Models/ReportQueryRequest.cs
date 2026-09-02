using Unified.Common.Reporting;

namespace Unified.Reporting.Models;

public sealed record ReportQueryRequest(
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> Filters,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Asc
);
