using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public interface IAssignmentService
{
    Task<IReadOnlyCollection<AssignmentSeriesResponse>> GetAssignmentSeriesAsync(
        AssignmentSeriesQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    );

    Task<AssignmentSeriesResponse?> GetAssignmentSeriesByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<AssignmentSeriesResponse> CreateAssignmentSeriesAsync(
        AssignmentSeriesRequest request,
        CancellationToken cancellationToken = default
    );

    Task<AssignmentSeriesResponse?> UpdateAssignmentSeriesAsync(
        int id,
        AssignmentSeriesRequest request,
        CancellationToken cancellationToken = default
    );

    Task<AssignmentSeriesResponse?> ExpireAssignmentSeriesAsync(
        int id,
        ExpireShiftRequest request,
        Guid? cancelledByUserId = null,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<AssignmentEntryResponse>> GetAssignmentEntriesAsync(
        AssignmentEntryQueryParams? queryParams = null,
        CancellationToken cancellationToken = default
    );

    Task<AssignmentEntryResponse?> GetAssignmentEntryByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<AssignmentEntryResponse> CreateAssignmentEntryAsync(
        AssignmentEntryRequest request,
        CancellationToken cancellationToken = default
    );

    Task<AssignmentEntryResponse?> UpdateAssignmentEntryAsync(
        int id,
        AssignmentEntryRequest request,
        CancellationToken cancellationToken = default
    );

    Task<AssignmentEntryResponse?> ExpireAssignmentEntryAsync(
        int id,
        ExpireShiftRequest request,
        Guid? cancelledByUserId = null,
        CancellationToken cancellationToken = default
    );
}
