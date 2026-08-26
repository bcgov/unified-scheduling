using Microsoft.Extensions.Logging;
using Unified.Common.Reporting;
using Unified.Reporting.Models;
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

    public async Task<PaginatableResponse> ExecuteAsync(
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

        return await handler.ExecuteAsync(
            request.Filters,
            request.Page,
            request.PageSize,
            request.SortBy,
            request.SortDirection.ToString(),
            request.TimeZone,
            cancellationToken
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

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}