using Unified.Leave.Models.Scheduling;

namespace Unified.Leave.Services;

public interface ISchedulingLeaveSeriesService
{
    Task<IReadOnlyList<SchedulingLeaveSeriesResponse>> GetLeaveSeriesByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );

    Task<SchedulingLeaveSeriesResponse?> GetLeaveSeriesByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<SchedulingLeaveSeriesResponse> CreateLeaveSeriesAsync(
        SchedulingLeaveSeriesRequest request,
        CancellationToken cancellationToken = default
    );

    Task<SchedulingLeaveSeriesResponse?> UpdateLeaveSeriesAsync(
        int id,
        SchedulingLeaveSeriesRequest request,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteLeaveSeriesAsync(int id, CancellationToken cancellationToken = default);
}
