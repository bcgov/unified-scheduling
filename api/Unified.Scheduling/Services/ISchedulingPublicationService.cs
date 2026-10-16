using Unified.Db.Models.Scheduling;

namespace Unified.Scheduling.Services;

public interface IShiftPublicationService
{
    Task<IReadOnlyCollection<ShiftPublicationBlocker>> GetEntryPublicationBlockersAsync(
        IReadOnlyCollection<int> shiftEntryIds,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a Serializable transaction when invoked standalone. When an EF transaction is already active,
    /// participates in the caller-owned transaction without committing it.
    /// </summary>
    Task<ShiftPublicationResult> PublishEntriesAsync(
        IReadOnlyCollection<int> shiftEntryIds,
        CancellationToken cancellationToken = default
    );
}

public interface IAssignmentPublicationService
{
    /// <summary>
    /// Creates a Serializable transaction when invoked standalone. When an EF transaction is already active,
    /// participates in the caller-owned transaction without committing it.
    /// </summary>
    Task<IReadOnlyCollection<AssignmentEntry>> PublishEntriesAsync(
        IReadOnlyCollection<int> assignmentEntryIds,
        CancellationToken cancellationToken = default
    );
}

public sealed record ShiftPublicationBlocker(string Message);

public sealed record ShiftPublicationResult(
    IReadOnlyCollection<ShiftEntry> ShiftEntries,
    IReadOnlyCollection<int> PublishedLinkedAssignmentEventIds
);
