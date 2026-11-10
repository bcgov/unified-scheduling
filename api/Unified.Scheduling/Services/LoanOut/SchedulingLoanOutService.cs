using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Conflicts;
using Unified.Common.Calendar.Conflicts;
using Unified.Db;
using Unified.Db.Models.LoanOut;

namespace Unified.Scheduling.Services;

public sealed class SchedulingLoanOutService(
    ShiftLoanOutAvailabilityService availabilityService,
    ShiftLoanOutExceptionMarkerService markerService,
    LoanOutShiftTransferService transferService,
    UnifiedDbContext db,
    ICalendarConflictService conflictService
) : ISchedulingLoanOutService
{
    public async Task InvalidateResolvedConflictOverridesAsync(
        IReadOnlyCollection<LoanOutEntry> entries,
        CancellationToken cancellationToken = default
    )
    {
        if (!availabilityService.IsEnabled || entries.Count == 0)
            return;

        var occurrences = entries.Where(entry => entry.Event is not null && entry.Users.Count > 0).ToList();
        if (occurrences.Count == 0)
            return;
        var userIds = occurrences.SelectMany(entry => entry.Users).Select(user => user.UserId).Distinct().ToList();
        // Participation uses the shift's local start day, which can extend beyond the loan-out's UTC interval.
        var earliest = occurrences.Min(entry => entry.Event!.StartAtUtc);
        var latest = occurrences.Max(entry => entry.Event!.EndAtUtc ?? entry.Event.StartAtUtc);
        var start = earliest < DateTimeOffset.MinValue.AddDays(2) ? DateTimeOffset.MinValue : earliest.AddDays(-2);
        var end = latest > DateTimeOffset.MaxValue.AddDays(-2) ? DateTimeOffset.MaxValue : latest.AddDays(2);
        var shifts = await db
            .ShiftEntries.AsNoTracking()
            .Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry =>
                entry.Event != null
                && entry.Event.StartAtUtc >= start
                && entry.Event.StartAtUtc < end
                && entry.Users.Any(user => userIds.Contains(user.UserId))
            )
            .ToListAsync(cancellationToken);
        var awayUsers = await availabilityService.GetAwayUserIdsAsync(shifts, cancellationToken);
        var otherwiseAwayUsers = await availabilityService.GetAwayUserIdsAsync(
            shifts,
            cancellationToken,
            occurrences.Select(entry => entry.Id).ToList()
        );
        var affectedShifts = shifts
            .Where(shift =>
                (awayUsers.GetValueOrDefault(shift) ?? [])
                    .Except(otherwiseAwayUsers.GetValueOrDefault(shift) ?? [])
                    .Any()
            )
            .ToList();
        var shiftIds = affectedShifts.Select(shift => shift.Id).ToList();
        var assignmentEventIds = await db
            .ShiftAssignmentEntries.Where(link => shiftIds.Contains(link.ShiftEntryId))
            .Select(link => link.AssignmentEntry!.EventId)
            .Distinct()
            .ToListAsync(cancellationToken);
        await conflictService.InvalidateResolvedOverridesAsync(
            affectedShifts
                .Select(shift => shift.EventId)
                .Concat(assignmentEventIds)
                .Distinct()
                .Select(eventId => new CalendarConflictEventIdentity(
                    SchedulingConstants.SourceModule,
                    eventId.ToString()
                ))
                .ToList(),
            cancellationToken: cancellationToken
        );
    }

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
