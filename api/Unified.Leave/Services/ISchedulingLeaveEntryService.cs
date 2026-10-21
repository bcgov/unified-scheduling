using Unified.Leave.Models.Scheduling;

namespace Unified.Leave.Services;

public interface ISchedulingLeaveEntryService
{
    Task<SchedulingLeaveEntryResponse?> GetLeaveEntryByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchedulingLeaveEntryResponse>> GetLeaveEntriesByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );

    Task<SchedulingLeaveEntryResponse> CreateLeaveEntryAsync(
        SchedulingLeaveEntryRequest request,
        CancellationToken cancellationToken = default
    );

    Task<SchedulingLeaveEntryResponse?> UpdateLeaveEntryAsync(
        int id,
        SchedulingLeaveEntryRequest request,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteLeaveEntryAsync(int id, CancellationToken cancellationToken = default);
}
