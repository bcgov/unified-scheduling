using Unified.Db.Models.LoanOut;

namespace Unified.Scheduling.Services;

public sealed class NoOpSchedulingLoanOutService : ISchedulingLoanOutService
{
    public Task RestoreHomeLocationUserShiftsAsync(
        IReadOnlyCollection<LoanOutEntry> removedEntries,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IReadOnlyCollection<Guid>>> GetAssignableUserIdsByOccurrenceAsync(
        IReadOnlyCollection<Guid> userIds,
        IReadOnlyCollection<ShiftAssignmentLoanOutContext> assignmentOccurrences,
        int? locationId,
        string? timeZoneId,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<IReadOnlyCollection<Guid>>>(
            assignmentOccurrences.Select(_ => userIds).ToList()
        );
    }

    public Task<bool> HasShiftAtLoanOutLocationAsync(
        IEnumerable<LoanOutEntry> loanOutEntries,
        int loanOutLocationId,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }

    public Task<bool> TransferLoanedOutUserShift(
        LoanOutEntry entry,
        bool deleteAssignments = false,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }

    public Task<bool> TransferLoanedOutUserShift(
        LoanOutSeries series,
        bool deleteAssignments = false,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }
}
