using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public interface IAssignmentEntryService
{
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
        AssignmentEntryUpdateRequest request,
        CancellationToken cancellationToken = default,
        Guid? conflictOverrideActorId = null
    );

    Task<AssignmentEntryResponse?> PublishAssignmentEntryAsync(int id, CancellationToken cancellationToken = default);

    Task<AssignmentEntryResponse?> ExpireAssignmentEntryAsync(
        int id,
        ExpireShiftRequest request,
        Guid? cancelledByUserId = null,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteAssignmentEntryAsync(int id, CancellationToken cancellationToken = default);
}
