using Unified.Reporting.Models.Reporting;

namespace Unified.Reporting.Services.Reporting;

public interface IReportQueryService
{
    Task<ReportQueryResult> ExecuteAsync(
        string reportKey,
        ReportQueryRequest request,
        CancellationToken cancellationToken = default
    );
}