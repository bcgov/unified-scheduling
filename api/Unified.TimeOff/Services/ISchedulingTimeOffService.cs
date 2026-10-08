using Unified.TimeOff.Models.Scheduling;

namespace Unified.TimeOff.Services;

public interface ISchedulingTimeOffService
{
    Task<SchedulingTimeOffEntryResponse?> GetTimeOffEntryByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<SchedulingTimeOffEntryResponse>> GetTimeOffEntriesByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<SchedulingTimeOffSeriesResponse>> GetTimeOffSeriesByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );

    Task<SchedulingTimeOffEntryResponse> CreateTimeOffEntryAsync(
        SchedulingTimeOffEntryRequest request,
        CancellationToken cancellationToken = default
    );

    Task<SchedulingTimeOffEntryResponse?> UpdateTimeOffEntryAsync(
        int id,
        SchedulingTimeOffEntryRequest request,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteTimeOffEntryAsync(int id, CancellationToken cancellationToken = default);

    Task<SchedulingTimeOffSeriesResponse?> GetTimeOffSeriesByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    );

    Task<SchedulingTimeOffSeriesResponse> CreateTimeOffSeriesAsync(
        SchedulingTimeOffSeriesRequest request,
        CancellationToken cancellationToken = default
    );

    Task<SchedulingTimeOffSeriesResponse?> UpdateTimeOffSeriesAsync(
        int id,
        SchedulingTimeOffSeriesRequest request,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteTimeOffSeriesAsync(int id, CancellationToken cancellationToken = default);
}
