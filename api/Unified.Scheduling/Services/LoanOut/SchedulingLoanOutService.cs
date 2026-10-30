using Unified.Db.Models.LoanOut;

namespace Unified.Scheduling.Services;

public sealed class SchedulingLoanOutService(
    ShiftLoanOutAvailabilityService availabilityService,
    ShiftLoanOutExceptionMarkerService markerService,
    LoanOutShiftTransferService transferService
) : ISchedulingLoanOutService
{
    public Task RestoreHomeLocationUserShiftsAsync(
        IReadOnlyCollection<LoanOutEntry> removedEntries,
        CancellationToken cancellationToken = default
    ) => markerService.RestoreHomeLocationUserShiftsAsync(removedEntries, cancellationToken);

    public Task<IReadOnlyList<IReadOnlyCollection<Guid>>> GetAssignableUserIdsByOccurrenceAsync(
        IReadOnlyCollection<Guid> userIds,
        IReadOnlyCollection<ShiftAssignmentLoanOutContext> assignmentOccurrences,
        int? locationId,
        string? timeZoneId,
        CancellationToken cancellationToken = default
    ) =>
        availabilityService.GetAssignableUserIdsByOccurrenceAsync(
            userIds,
            assignmentOccurrences,
            locationId,
            timeZoneId,
            cancellationToken
        );

    public Task<bool> HasShiftAtLoanOutLocationAsync(
        IEnumerable<LoanOutEntry> loanOutEntries,
        int loanOutLocationId,
        CancellationToken cancellationToken
    ) => availabilityService.HasShiftAtLoanOutLocationAsync(loanOutEntries, loanOutLocationId, cancellationToken);

    public Task<bool> TransferLoanedOutUserShift(
        LoanOutEntry entry,
        bool deleteAssignments = false,
        CancellationToken cancellationToken = default
    ) => transferService.TransferLoanedOutUserShift(entry, cancellationToken);

    public Task<bool> TransferLoanedOutUserShift(
        LoanOutSeries series,
        bool deleteAssignments = false,
        CancellationToken cancellationToken = default
    ) => transferService.TransferLoanedOutUserShift(series, cancellationToken);
}
