using Unified.Core.Models.Reporting;

namespace Unified.Core.Services.Reporting;

public interface IReportQueryService
{
    Task<ReportQueryResult> ExecuteAsync(
        string reportKey,
        ReportQueryRequest request,
        CancellationToken cancellationToken = default
    );
}
