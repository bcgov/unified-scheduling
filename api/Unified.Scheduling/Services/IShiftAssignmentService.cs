using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public interface IShiftAssignmentService
{
    Task<ShiftAssignmentEntryResponse> LinkShiftEntryAsync(
        ShiftAssignmentEntryRequest request,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<ShiftAssignmentEntryResponse>> LinkShiftSeriesAsync(
        ShiftAssignmentSeriesRequest request,
        CancellationToken cancellationToken = default
    );
}
