namespace Unified.Common.Reporting;

public interface IReportQueryHandler
{
    string ReportKey { get; }

    Task<PagedResponse> ExecuteAsync(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters,
        string? sortBy,
        SortDirection sortDirection,
        CancellationToken cancellationToken = default
    );
}
