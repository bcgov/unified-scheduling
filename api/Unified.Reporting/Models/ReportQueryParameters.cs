namespace Unified.Reporting.Models;

/// <summary>
/// Shared query parameters supported by all report endpoints.
/// Report-specific filters are passed as additional query keys.
/// </summary>
public sealed record ReportQueryParameters(
    int? Page = null,
    int? PageSize = null,
    string? SortBy = null,
    string? SortDir = null,
    Dictionary<string, string?>? Filters = null
);
