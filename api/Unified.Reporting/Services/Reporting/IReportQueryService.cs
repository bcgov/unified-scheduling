using Unified.Common.Reporting;
using Unified.Reporting.Models.Reporting;

namespace Unified.Reporting.Services.Reporting;

public interface IReportQueryService
{
    Task<PaginatableResponse> ExecuteAsync(
        string reportKey,
        ReportQueryRequest request,
        CancellationToken cancellationToken = default
    );
}