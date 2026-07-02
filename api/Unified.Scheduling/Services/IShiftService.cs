using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public interface IShiftService
{
    Task<IReadOnlyCollection<ShiftSeriesResponse>> GetShiftSeriesAsync(
        ShiftSeriesQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    );

    Task<ShiftSeriesResponse?> GetShiftSeriesByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ShiftSeriesResponse> CreateShiftSeriesAsync(
        ShiftSeriesRequest request,
        CancellationToken cancellationToken = default
    );

    Task<ShiftSeriesResponse?> UpdateShiftSeriesAsync(
        int id,
        ShiftSeriesRequest request,
        CancellationToken cancellationToken = default
    );

    Task<ShiftSeriesResponse?> PublishShiftSeriesAsync(int id, CancellationToken cancellationToken = default);

    Task<ShiftSeriesResponse?> ExpireShiftSeriesAsync(
        int id,
        ExpireShiftRequest request,
        Guid? cancelledByUserId = null,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteShiftSeriesAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ShiftEntryResponse>> GetShiftEntriesAsync(
        ShiftEntryQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    );

    Task<ShiftEntryResponse?> GetShiftEntryByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ShiftEntryResponse> CreateShiftEntryAsync(
        ShiftEntryRequest request,
        CancellationToken cancellationToken = default
    );

    Task<ShiftEntryResponse?> UpdateShiftEntryAsync(
        int id,
        ShiftEntryRequest request,
        CancellationToken cancellationToken = default
    );

    Task<ShiftEntryResponse?> PublishShiftEntryAsync(int id, CancellationToken cancellationToken = default);

    Task<ShiftEntryResponse?> ExpireShiftEntryAsync(
        int id,
        ExpireShiftRequest request,
        Guid? cancelledByUserId = null,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteShiftEntryAsync(int id, CancellationToken cancellationToken = default);

    Task<SchedulingCalendarDataResponse> GetSchedulingCalendarDataAsync(
        SchedulingCalendarRequest request,
        CancellationToken cancellationToken = default
    );
}
