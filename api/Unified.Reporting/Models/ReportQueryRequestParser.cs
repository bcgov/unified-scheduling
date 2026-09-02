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
        "tz",
    };

    public static ReportQueryRequest FromQuery(IQueryCollection query)
    {
        var sortBy = GetValueOrNull(query, "sortBy");
        var sortDirection = ParseSortDirectionOrDefault(query, "sortDir", SortDirection.Asc);

        var filters = query
            .Where(entry => !ReservedQueryKeys.Contains(entry.Key))
            .ToDictionary(
                entry => entry.Key,
                entry =>
                    (IReadOnlyCollection<string>)
                        [.. entry.Value.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!)],
                StringComparer.OrdinalIgnoreCase
            );

        return new ReportQueryRequest(filters, sortBy, sortDirection);
    }

    private static SortDirection ParseSortDirectionOrDefault(IQueryCollection query, string key, SortDirection fallback)
    {
        var value = GetValueOrNull(query, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (Enum.TryParse<SortDirection>(value, true, out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"Query parameter '{key}' must be either 'asc' or 'desc'.");
    }

    private static string? GetValueOrNull(IQueryCollection query, string key)
    {
        if (!query.TryGetValue(key, out var value))
        {
            return null;
        }

        var resolved = value.FirstOrDefault();
        return string.IsNullOrWhiteSpace(resolved) ? null : resolved;
    }
}
