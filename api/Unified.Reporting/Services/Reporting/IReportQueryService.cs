using Unified.Common.Reporting;
using Unified.Reporting.Models;

namespace Unified.Reporting.Services.Reporting;

public interface IReportQueryService
{
    Task<PagedResponse> ExecuteAsync(
        string reportKey,
        ReportQueryRequest request,
        CancellationToken cancellationToken = default
    );
}