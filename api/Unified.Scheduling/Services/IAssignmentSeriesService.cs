using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public interface IAssignmentSeriesService
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

    Task<AssignmentSeriesResponse?> PublishAssignmentSeriesAsync(int id, CancellationToken cancellationToken = default);

    Task<AssignmentSeriesResponse?> ExpireAssignmentSeriesAsync(
        int id,
        ExpireShiftRequest request,
        Guid? cancelledByUserId = null,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteAssignmentSeriesAsync(int id, CancellationToken cancellationToken = default);
}
