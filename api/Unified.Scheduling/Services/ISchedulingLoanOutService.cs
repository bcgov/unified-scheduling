using Unified.Db.Models.LoanOut;

namespace Unified.Scheduling.Services;

public interface ISchedulingLoanOutService
{
    // Each result corresponds to the assignment at the same index in the input.
    Task<IReadOnlyList<IReadOnlyCollection<Guid>>> FilterLoanedOutUserAssignmentsAsync(
        IReadOnlyCollection<Guid> userIds,
        IReadOnlyCollection<ShiftAssignmentLoanOutContext> assignments,
        int? locationId,
        string? timeZoneId,
        CancellationToken cancellationToken = default
    );

    Task EnsureNoShiftAtLoanOutLocationAsync(
        IEnumerable<LoanOutEntry> loanOutEntries,
        int loanOutLocationId,
        CancellationToken cancellationToken = default
    );

    Task<bool> TransferLoanedOutUserShift(
        LoanOutEntry entry,
        bool deleteAssignments = false,
        CancellationToken cancellationToken = default
    );

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
