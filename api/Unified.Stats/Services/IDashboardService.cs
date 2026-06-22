using Unified.Stats.Models;

namespace Unified.Stats.Services;

public interface IDashboardService
{
    Task<IReadOnlyCollection<DashboardEntryResponse>> GetEntriesAsync(
        int callerHomeLocationId,
        DashboardEntriesQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    );

    Task<DashboardSummaryResponse> GetSummaryAsync(
        int callerHomeLocationId,
        DashboardEntriesQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    );
}
