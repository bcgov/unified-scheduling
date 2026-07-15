using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public interface IShiftAssignmentService
{
    Task<ShiftAssignmentEntryResponse> LinkShiftEntryAsync(
        ShiftAssignmentEntryRequest request,
        CancellationToken cancellationToken = default
    );

    Task<ShiftAssignmentEntryResponse> UpsertShiftEntryLinkAsync(
        ShiftAssignmentEntryRequest request,
        CancellationToken cancellationToken = default
    );

    Task<ShiftAssignmentSeriesLinkResponse> LinkShiftSeriesAsync(
        ShiftAssignmentSeriesRequest request,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<ShiftAssignmentEntryResponse>> UpsertShiftEntryLinksAsync(
        int shiftEntryId,
        IReadOnlyCollection<int>? assignmentEntryIds,
        IReadOnlyCollection<Guid>? assignedUserIds,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<ShiftAssignmentEntryResponse>> UpsertShiftEntryLinksAsync(
        int shiftEntryId,
        IReadOnlyCollection<AssignmentEntryLinkRequest>? assignmentEntryLinks,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<ShiftAssignmentEntryResponse>> LinkShiftSeriesAsync(
        int shiftSeriesId,
        IReadOnlyCollection<int>? assignmentSeriesIds,
        IReadOnlyCollection<Guid>? assignedUserIds,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<ShiftAssignmentEntryResponse>> LinkShiftSeriesAsync(
        int shiftSeriesId,
        IReadOnlyCollection<AssignmentSeriesLinkRequest>? assignmentSeriesLinks,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<ShiftAssignmentEntryResponse>> UpsertAssignmentEntryLinksAsync(
        int assignmentEntryId,
        IReadOnlyCollection<int>? shiftEntryIds,
        IReadOnlyCollection<Guid>? assignedUserIds,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<ShiftAssignmentEntryResponse>> UpsertAssignmentEntryLinksAsync(
        int assignmentEntryId,
        IReadOnlyCollection<ShiftEntryLinkRequest>? shiftEntryLinks,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<ShiftAssignmentEntryResponse>> LinkAssignmentSeriesAsync(
        int assignmentSeriesId,
        IReadOnlyCollection<int>? shiftSeriesIds,
        IReadOnlyCollection<Guid>? assignedUserIds,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<ShiftAssignmentEntryResponse>> LinkAssignmentSeriesAsync(
        int assignmentSeriesId,
        IReadOnlyCollection<ShiftSeriesLinkRequest>? shiftSeriesLinks,
        CancellationToken cancellationToken = default
    );
}
