using Microsoft.AspNetCore.Http;
using Unified.Common.Reporting;

namespace Unified.Reporting.Models;

internal static class ReportQueryRequestParser
{
    private static readonly HashSet<string> ReservedQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "page",
        "pageSize",
        "sortBy",
        "sortDir",
        "filters",
    };

    public static ReportQueryRequest FromQuery(IQueryCollection query, ReportQueryParameters? parameters = null)
    {
        parameters ??= new ReportQueryParameters();

        var sortBy = string.IsNullOrWhiteSpace(parameters.SortBy) ? null : parameters.SortBy;
        var sortDirection = ParseSortDirectionOrDefault(parameters.SortDir, SortDirection.Asc);

        var filters = query
            .Where(entry => !ReservedQueryKeys.Contains(entry.Key))
            .Where(entry => !IsFiltersQueryKey(entry.Key))
            .ToDictionary(
                entry => entry.Key,
                entry =>
                    (IReadOnlyCollection<string>)
                        [.. entry.Value.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!)],
                StringComparer.OrdinalIgnoreCase
            );

        return new ReportQueryRequest(filters, sortBy, sortDirection);
    }

    private static bool IsFiltersQueryKey(string key) =>
        key.StartsWith("filters[", StringComparison.OrdinalIgnoreCase) && key.EndsWith(']');

    private static SortDirection ParseSortDirectionOrDefault(string? value, SortDirection fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (Enum.TryParse<SortDirection>(value, true, out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException("Query parameter 'sortDir' must be either 'asc' or 'desc'.");
    }
}
