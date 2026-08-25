using Microsoft.Extensions.Logging;
using Unified.Reporting.Models.Reporting;
using CommonReporting = Unified.Common.Reporting;

namespace Unified.Reporting.Services.Reporting;

public sealed class ReportQueryService(
    IEnumerable<CommonReporting.IReportQueryHandler> handlers,
    ILogger<ReportQueryService> logger
) : IReportQueryService
{
    private const int MaxPageSize = 500;

    private readonly Dictionary<string, CommonReporting.IReportQueryHandler> _handlers = handlers
        .GroupBy(handler => Normalize(handler.ReportKey))
        .ToDictionary(
            group => group.Key,
            group =>
                group.Count() > 1
                    ? throw new InvalidOperationException(
                        $"Duplicate report handler registration for '{group.Key}'."
                    )
                    : group.Single()
        );

    public async Task<ReportQueryResult> ExecuteAsync(
        string reportKey,
        ReportQueryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(reportKey))
        {
            throw new ArgumentException("Report key is required.", nameof(reportKey));
        }

        ValidateRequest(request);

        var normalizedKey = Normalize(reportKey);
        if (!_handlers.TryGetValue(normalizedKey, out var handler))
        {
            throw new KeyNotFoundException($"Report '{reportKey}' is not supported.");
        }

        logger.LogDebug("Executing report query for {ReportKey}", normalizedKey);

        var (columns, rows, totalRows) = await handler.ExecuteAsync(
            request.Filters,
            request.Page,
            request.PageSize,
            request.SortBy,
            request.SortDirection.ToString(),
            request.TimeZone,
            cancellationToken
        );

        return new ReportQueryResult(
            reportKey,
            MapColumns(columns),
            rows,
            request.Page,
            request.PageSize,
            totalRows
        );
    }

    private static void ValidateRequest(ReportQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Page), request.Page, "Page must be greater than 0.");
        }

        if (request.PageSize < 1 || request.PageSize > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.PageSize),
                request.PageSize,
                $"Page size must be between 1 and {MaxPageSize}."
            );
        }
    }

    private static IReadOnlyCollection<ReportColumn> MapColumns(
        IReadOnlyCollection<IReadOnlyDictionary<string, object?>> columns
    )
    {
        return columns
            .Select(column =>
                new ReportColumn(
                    GetRequiredString(column, "key"),
                    GetRequiredString(column, "label"),
                    ParseValueType(GetRequiredString(column, "type")),
                    GetBooleanOrDefault(column, "sortable", true)
                )
            )
            .ToArray();
    }

    private static string GetRequiredString(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            throw new ArgumentException($"Report column is missing required '{key}' value.");
        }

        var resolved = value.ToString();
        if (string.IsNullOrWhiteSpace(resolved))
        {
            throw new ArgumentException($"Report column contains empty '{key}' value.");
        }

        return resolved;
    }

    private static bool GetBooleanOrDefault(
        IReadOnlyDictionary<string, object?> values,
        string key,
        bool defaultValue
    )
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return defaultValue;
        }

        if (value is bool typed)
        {
            return typed;
        }

        return bool.TryParse(value.ToString(), out var parsed) ? parsed : defaultValue;
    }

    private static ReportValueType ParseValueType(string rawType)
    {
        if (Enum.TryParse<ReportValueType>(rawType, true, out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"Unsupported report column type '{rawType}'.");
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}