using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public interface IShiftSeriesService
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

    Task<ShiftSeries?> GetShiftSeriesEntityForEntryAsync(int id, CancellationToken cancellationToken = default);
}
