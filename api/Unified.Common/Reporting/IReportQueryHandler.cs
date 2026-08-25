namespace Unified.Common.Reporting;

public interface IReportQueryHandler
{
    string ReportKey { get; }

    Task<PaginatableResponse> ExecuteAsync(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters,
        int page,
        int pageSize,
        string? sortBy,
        string? sortDirection,
        string? timeZone,
        CancellationToken cancellationToken = default
    );
}