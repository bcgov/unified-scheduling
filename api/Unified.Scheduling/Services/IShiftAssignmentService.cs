using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public interface IShiftAssignmentService
{
    Task<ShiftAssignmentEntryResponse> LinkShiftEntryAsync(
        ShiftAssignmentEntryRequest request,
        CancellationToken cancellationToken = default
    );

    Task<ShiftAssignmentEntryResponse?> UpdateShiftEntryLinkAsync(
        int id,
        ShiftAssignmentEntryUpdateRequest request,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteShiftEntryLinkAsync(int id, CancellationToken cancellationToken = default);

    Task<ShiftAssignmentSeriesLinkResponse> LinkShiftSeriesAsync(
        ShiftAssignmentSeriesRequest request,
        CancellationToken cancellationToken = default
    );

    Task<ShiftAssignmentSeriesLinkResponse?> UpdateShiftSeriesLinkAsync(
        int id,
        ShiftAssignmentSeriesUpdateRequest request,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteShiftSeriesLinkAsync(int id, CancellationToken cancellationToken = default);
}
