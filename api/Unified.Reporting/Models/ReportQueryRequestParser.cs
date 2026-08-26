using Microsoft.AspNetCore.Http;

namespace Unified.Reporting.Models;

internal static class ReportQueryRequestParser
{
    private static readonly HashSet<string> ReservedQueryKeys =
    ["page", "pagesize", "sortby", "sortdir", "tz"];

    public static ReportQueryRequest FromQuery(IQueryCollection query)
    {
        var page = ParseIntOrDefault(query, "page", 1);
        var pageSize = ParseIntOrDefault(query, "pageSize", 50);
        var sortBy = GetValueOrNull(query, "sortBy");
        var sortDirection = ParseSortDirectionOrDefault(query, "sortDir", SortDirection.Asc);
        var timeZone = GetValueOrNull(query, "tz");

        var filters = query
            .Where(entry => !ReservedQueryKeys.Contains(entry.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(
                entry => entry.Key,
                entry =>
                    (IReadOnlyCollection<string>)[.. entry
                        .Value.Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!)],
                StringComparer.OrdinalIgnoreCase
            );

        return new ReportQueryRequest(filters, page, pageSize, sortBy, sortDirection, timeZone);
    }

    private static int ParseIntOrDefault(IQueryCollection query, string key, int fallback)
    {
        var value = GetValueOrNull(query, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (!int.TryParse(value, out var parsed))
        {
            throw new ArgumentException($"Query parameter '{key}' must be an integer.");
        }

        return parsed;
    }

    private static SortDirection ParseSortDirectionOrDefault(
        IQueryCollection query,
        string key,
        SortDirection fallback
    )
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
