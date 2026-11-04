using Unified.Db.Models.LoanOut;

namespace Unified.Scheduling.Services;

public interface ISchedulingLoanOutService
{
    /// <summary>Clears home-shift away markers for occurrences being deleted, preserving other active loan-outs.</summary>
    Task RestoreHomeLocationUserShiftsAsync(
        IReadOnlyCollection<LoanOutEntry> removedEntries,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<IReadOnlyCollection<Guid>>> GetAssignableUserIdsByOccurrenceAsync(
        IReadOnlyCollection<Guid> userIds,
        IReadOnlyCollection<ShiftAssignmentLoanOutContext> assignmentOccurrences,
        int? locationId,
        string? timeZoneId,
        CancellationToken cancellationToken = default
    );

    Task<bool> HasShiftAtLoanOutLocationAsync(
        IEnumerable<LoanOutEntry> loanOutEntries,
        int loanOutLocationId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Copies overlapping shifts as standalone entries, including source-series occurrences.
    /// Source shift memberships and assignment records are preserved. Only transferred users are delinked from assignments.
    /// deleteAssignments is retained for caller compatibility and never deletes assignment records.
    /// </summary>
    Task<bool> TransferLoanedOutUserShift(
        LoanOutEntry entry,
        bool deleteAssignments = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Transfers shifts using the original series and its materialized entries.
    /// The series EventSeries carries the recurrence rule for the destination series.
    /// Source shift memberships and assignment records are preserved. Only transferred users are delinked from assignments.
    /// deleteAssignments is retained for caller compatibility and never deletes assignment records.
    /// </summary>
    Task<bool> TransferLoanedOutUserShift(
        LoanOutSeries series,
        bool deleteAssignments = false,
        CancellationToken cancellationToken = default
    );
}

public sealed record ShiftAssignmentLoanOutContext(
    DateTimeOffset AssignmentStartAtUtc,
    DateTimeOffset? AssignmentEndAtUtc
);
