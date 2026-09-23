using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public interface IShiftAssignmentService
{
    Task<ShiftAssignmentEntryResponse> LinkShiftEntryAsync(
        ShiftAssignmentEntryRequest request,
        CancellationToken cancellationToken = default,
        Guid? conflictOverrideActorId = null
    );

    Task<ShiftAssignmentEntryResponse?> UpdateShiftEntryLinkAsync(
        int id,
        ShiftAssignmentEntryUpdateRequest request,
        CancellationToken cancellationToken = default,
        Guid? conflictOverrideActorId = null
    );

    Task<bool> DeleteShiftEntryLinkAsync(int id, CancellationToken cancellationToken = default);

    Task ReplaceShiftEntryLinksAsync(
        int shiftEntryId,
        IReadOnlyCollection<AssignmentEntryLinkRequest>? links,
        CancellationToken cancellationToken = default
    );

    Task ReplaceAssignmentEntryLinksAsync(
        int assignmentEntryId,
        IReadOnlyCollection<ShiftEntryLinkRequest>? links,
        CancellationToken cancellationToken = default
    );

    Task<ShiftAssignmentSeriesLinkResponse> LinkShiftSeriesAsync(
        ShiftAssignmentSeriesRequest request,
        CancellationToken cancellationToken = default,
        Guid? conflictOverrideActorId = null
    );

    Task<ShiftAssignmentSeriesLinkResponse?> UpdateShiftSeriesLinkAsync(
        int id,
        ShiftAssignmentSeriesUpdateRequest request,
        CancellationToken cancellationToken = default,
        Guid? conflictOverrideActorId = null
    );

    Task<bool> DeleteShiftSeriesLinkAsync(int id, CancellationToken cancellationToken = default);

    Task ReplaceShiftSeriesLinksAsync(
        int shiftSeriesId,
        IReadOnlyCollection<AssignmentSeriesLinkRequest> links,
        CancellationToken cancellationToken = default
    );

    Task ReplaceAssignmentSeriesLinksAsync(
        int assignmentSeriesId,
        IReadOnlyCollection<ShiftSeriesLinkRequest> links,
        CancellationToken cancellationToken = default
    );
}
