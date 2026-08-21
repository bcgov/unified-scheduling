using Unified.Core.Models.Reporting;

namespace Unified.Core.Services.Reporting;

public interface IReportQueryHandler
{
    static abstract string ReportKey { get; }

    Task<ReportQueryResult> ExecuteAsync(
        ReportQueryRequest request,
        CancellationToken cancellationToken = default
    );
}
