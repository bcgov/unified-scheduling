using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Common.Validation;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Mappings;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class SchedulingLoanOutService(
    ILogger<SchedulingLoanOutService> logger,
    UnifiedDbContext db,
    SchedulingLoanOutEventMapper eventMapper,
    Func<IShiftService> shiftServiceFactory,
    Func<IAssignmentEntryService> assignmentEntryServiceFactory,
    Func<IShiftAssignmentService> shiftAssignmentServiceFactory,
    ICalendarTimeZoneResolver timeZoneResolver,
    ITimeZoneService timeZoneService
) : ISchedulingLoanOutService
{
    public async Task<IReadOnlyList<IReadOnlyCollection<Guid>>> FilterLoanedOutUserAssignmentsAsync(
        IReadOnlyCollection<Guid> userIds,
        IReadOnlyCollection<ShiftAssignmentLoanOutContext> assignments,
        int? locationId,
        string? timeZoneId,
        CancellationToken cancellationToken = default
    )
    {
        if (assignments.Count == 0)
            return [];

        var locationTimeZoneId = locationId.HasValue
            ? await db
                .Locations.AsNoTracking()
                .Where(location => location.Id == locationId.Value)
                .Select(location => location.Timezone)
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var timeZone = timeZoneResolver.Resolve(locationId.HasValue ? null : timeZoneId, locationTimeZoneId);
        var ranges = assignments.Select(assignment => GetAssignmentDayRange(assignment, timeZone)).ToList();

        var rangeStartAtUtc = ranges.Min(range => range.StartAtUtc);
        var rangeEndAtUtc = ranges.Max(range => range.EndAtUtc);
        IReadOnlyList<LoanOutEntry> loanOutEntries =
            userIds.Count == 0
                ? []
                : await db
                    .LoanOutEntries.AsNoTracking()
                    .Include(entry => entry.Event)
                    .Include(entry => entry.Users)
                    .Where(entry =>
                        entry.Event != null
                        && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                        && entry.Users.Any(user => userIds.Contains(user.UserId))
                        && entry.Event.StartAtUtc < rangeEndAtUtc
                        && (
                            entry.Event.EndAtUtc.HasValue
                                ? entry.Event.EndAtUtc.Value > rangeStartAtUtc
                                : entry.Event.StartAtUtc >= rangeStartAtUtc
                        )
                    )
                    .ToListAsync(cancellationToken);

        return ranges
            .Select(range =>
            {
                var loanedOutUserIds = loanOutEntries
                    .Where(entry =>
                        entry.Event is Event loanOutEvent && Overlaps(loanOutEvent, range.StartAtUtc, range.EndAtUtc)
                    )
                    .SelectMany(entry => entry.Users)
                    .Select(user => user.UserId)
                    .ToHashSet();
                return (IReadOnlyCollection<Guid>)userIds.Where(userId => !loanedOutUserIds.Contains(userId)).ToList();
            })
            .ToList();
    }

    public async Task EnsureNoShiftAtLoanOutLocationAsync(
        IEnumerable<LoanOutEntry> loanOutEntries,
        int loanOutLocationId,
        CancellationToken cancellationToken
    )
    {
        var entries = loanOutEntries.Where(entry => entry.Event is not null && entry.Users.Count > 0).ToList();
        if (entries.Count == 0)
            return;

        var locationTimeZoneId = await db
            .Locations.AsNoTracking()
            .Where(location => location.Id == loanOutLocationId)
            .Select(location => location.Timezone)
            .SingleOrDefaultAsync(cancellationToken);
        var timeZone = timeZoneResolver.Resolve(null, locationTimeZoneId);
        var ranges = entries
            .Select(entry => new
            {
                UserIds = entry.Users.Select(user => user.UserId).ToHashSet(),
                Range = GetAssignmentDayRange(
                    new ShiftAssignmentLoanOutContext(entry.Event!.StartAtUtc, entry.Event.EndAtUtc),
                    timeZone
                ),
            })
            .ToList();
        var rangeStartAtUtc = ranges.Min(item => item.Range.StartAtUtc);
        var rangeEndAtUtc = ranges.Max(item => item.Range.EndAtUtc);
        var shifts = await shiftServiceFactory()
            .GetShiftEntriesAsync(
                new ShiftEntryQueryParams
                {
                    LocationId = loanOutLocationId,
                    StartAtUtc = rangeStartAtUtc,
                    EndAtUtc = rangeEndAtUtc,
                },
                cancellationToken
            );

        if (
            shifts.Any(shift =>
                ranges.Any(item => shift.UserIds.Any(item.UserIds.Contains) && Overlaps(shift, item.Range))
            )
        )
        {
            throw new ConflictValidationException(
                new Dictionary<string, string[]>
                {
                    ["LoanOut"] =
                    [
                        "A shift exists at the loan-out location, so the loan-out cannot be updated or removed.",
                    ],
                }
            );
        }
    }

    public async Task<bool> TransferLoanedOutUserShift(
        LoanOutEntry entry,
        bool deleteAssignments = false,
        CancellationToken cancellationToken = default
    )
    {
        var processedIds = new HashSet<int>();
        var transferred = await TransferLoanOutEntriesAsync(
            [entry],
            entry.LoanOutLocationId,
            processedIds,
            deleteAssignments,
            cancellationToken
        );
        if (transferred)
            logger.LogInformation(
                "Created copied shifts for loan-out entry {LoanOutEntryId} at location {LocationId}.",
                entry.Id,
                entry.LoanOutLocationId
            );
        return transferred;
    }

    public async Task<bool> TransferLoanedOutUserShift(
        LoanOutSeries series,
        bool deleteAssignments = false,
        CancellationToken cancellationToken = default
    )
    {
        var processedIds = new HashSet<int>();
        var entries = series.LoanOutEntries.ToList();
        var transferred = await TransferLoanOutEntriesAsync(
            entries,
            series.LoanOutLocationId,
            processedIds,
            deleteAssignments,
            cancellationToken
        );

        logger.LogInformation(
            "Created shifts for loan-out series {LoanOutSeriesId}; transfer result {Transferred}.",
            series.Id,
            transferred
        );
        return transferred;
    }

    private async Task<bool> TransferLoanOutEntriesAsync(
        IReadOnlyCollection<LoanOutEntry> loanOutEntries,
        int loanOutLocationId,
        HashSet<int> processedIds,
        bool deleteAssignments,
        CancellationToken cancellationToken
    )
    {
        var entries = loanOutEntries
            .Where(entry => entry.Event is not null)
            .Select(entry => new
            {
                Event = entry.Event!,
                UserIds = entry.Users.Select(user => user.UserId).Distinct().ToList(),
            })
            .Where(entry => entry.UserIds.Count > 0)
            .ToList();
        if (entries.Count == 0)
            return false;

        var locationTimeZoneId = await db
            .Locations.AsNoTracking()
            .Where(location => location.Id == loanOutLocationId)
            .Select(location => location.Timezone)
            .SingleOrDefaultAsync(cancellationToken);
        var windows = entries
            .Select(entry =>
            {
                var occurrenceEnd = entry.Event.EndAtUtc ?? entry.Event.StartAtUtc;
                return new LoanOutTransferWindow(
                    timeZoneService.ToTimeZone(entry.Event.StartAtUtc, locationTimeZoneId).ToUniversalTime(),
                    timeZoneService.ToTimeZone(occurrenceEnd, locationTimeZoneId).ToUniversalTime(),
                    entry.UserIds
                );
            })
            .ToList();
        var rangeStartAtUtc = windows.Min(window => window.StartAtUtc);
        var rangeEndAtUtc = windows.Max(window => window.EndAtUtc);
        var userIds = windows.SelectMany(window => window.UserIds).Distinct().ToList();

        var query = db
            .ShiftEntries.Include(shift => shift.Event)
            .Include(shift => shift.ShiftSeries!)
                .ThenInclude(series => series.EventSeries)
            .Include(shift => shift.Users)
            .Include(shift => shift.ShiftAssignmentEntries)
                .ThenInclude(link => link.Users)
            .Where(shift =>
                shift.Event != null
                && shift.Users.Any(user => userIds.Contains(user.UserId))
                && shift.Event.StatusTypeCode == CalendarEventStatusTypeCodes.Draft
                && shift.Event.LocationId != loanOutLocationId
                && (
                    shift.Event.EndAtUtc.HasValue
                        ? shift.Event.EndAtUtc.Value > rangeStartAtUtc
                        : shift.Event.StartAtUtc >= rangeStartAtUtc
                )
                && shift.Event.StartAtUtc < rangeEndAtUtc
            );
        var shifts = await query.ToListAsync(cancellationToken);
        var matchingShifts = shifts
            .Select(shift =>
            {
                var eventEntity = shift.Event!;
                var shiftUserIds = shift.Users.Select(user => user.UserId).ToHashSet();
                var matchingUserIds = windows
                    .Where(window => Overlaps(eventEntity, window))
                    .SelectMany(window => window.UserIds)
                    .Where(shiftUserIds.Contains)
                    .Distinct()
                    .Order()
                    .ToList();
                return new ShiftTransfer(shift, matchingUserIds);
            })
            .Where(transfer => transfer.UserIds.Count > 0)
            .ToList();
        var transferred = false;

        foreach (
            var transfer in matchingShifts.Where(transfer =>
                string.IsNullOrWhiteSpace(transfer.Shift.ShiftSeries?.EventSeries?.RecurrenceRule)
            )
        )
        {
            await RemoveOrDelinkAssignmentLinksAsync(
                transfer.Shift,
                transfer.UserIds,
                processedIds,
                deleteAssignments,
                cancellationToken
            );
            var request = eventMapper.ToShiftEntryRequest(transfer.Shift, loanOutLocationId, transfer.UserIds);
            await shiftServiceFactory().CreateShiftEntryAsync(request, cancellationToken);
            transfer.Shift.Event!.IsException = true;
            transfer.Shift.Event.ExceptionReason = nameof(CalendarEventExceptionReasonCode.IsAway);
            transferred = true;
        }

        var recurringShifts = matchingShifts.Where(transfer =>
            !string.IsNullOrWhiteSpace(transfer.Shift.ShiftSeries?.EventSeries?.RecurrenceRule)
        );
        foreach (var seriesGroup in recurringShifts.GroupBy(transfer => transfer.Shift.ShiftSeriesId))
        {
            var matchingOccurrences = seriesGroup
                .Select(transfer => transfer.Shift)
                .DistinctBy(shift => shift.Id)
                .ToList();
            var loanedUserIds = seriesGroup.SelectMany(transfer => transfer.UserIds).Distinct().Order().ToList();
            foreach (var transfer in seriesGroup)
            {
                await RemoveOrDelinkAssignmentLinksAsync(
                    transfer.Shift,
                    transfer.UserIds,
                    processedIds,
                    deleteAssignments,
                    cancellationToken
                );
            }

            if (matchingOccurrences.Count == 1)
            {
                var occurrence = matchingOccurrences[0];
                var entryRequest = eventMapper.ToShiftEntryRequest(occurrence, loanOutLocationId, loanedUserIds);
                await shiftServiceFactory().CreateShiftEntryAsync(entryRequest, cancellationToken);
                occurrence.Event!.IsException = true;
                occurrence.Event.ExceptionReason = nameof(CalendarEventExceptionReasonCode.IsAway);
                transferred = true;
                continue;
            }

            var sourceSeries = seriesGroup.First().Shift.ShiftSeries!;
            var request = eventMapper.ToShiftSeriesRequest(
                sourceSeries,
                matchingOccurrences,
                loanOutLocationId,
                loanedUserIds
            );
            await shiftServiceFactory().CreateShiftSeriesAsync(request, cancellationToken);
            foreach (var occurrence in matchingOccurrences)
            {
                occurrence.Event!.IsException = true;
                occurrence.Event.ExceptionReason = nameof(CalendarEventExceptionReasonCode.IsAway);
            }
            transferred = true;
        }

        if (transferred)
            await db.SaveChangesAsync(cancellationToken);

        return transferred;
    }

    private static bool Overlaps(Event eventEntity, LoanOutTransferWindow window) =>
        eventEntity.EndAtUtc.HasValue
            ? eventEntity.EndAtUtc.Value > window.StartAtUtc && eventEntity.StartAtUtc < window.EndAtUtc
            : eventEntity.StartAtUtc >= window.StartAtUtc && eventEntity.StartAtUtc < window.EndAtUtc;

    private static bool Overlaps(ShiftEntryResponse shift, UtcDateRange range) =>
        shift.EndAtUtc.HasValue
            ? shift.EndAtUtc.Value > range.StartAtUtc && shift.StartAtUtc < range.EndAtUtc
            : shift.StartAtUtc >= range.StartAtUtc && shift.StartAtUtc < range.EndAtUtc;

    private static bool Overlaps(Event eventEntity, DateTimeOffset rangeStartAtUtc, DateTimeOffset rangeEndAtUtc) =>
        eventEntity.EndAtUtc.HasValue
            ? eventEntity.EndAtUtc.Value > rangeStartAtUtc && eventEntity.StartAtUtc < rangeEndAtUtc
            : eventEntity.StartAtUtc >= rangeStartAtUtc && eventEntity.StartAtUtc < rangeEndAtUtc;

    private UtcDateRange GetAssignmentDayRange(ShiftAssignmentLoanOutContext assignment, TimeZoneInfo timeZone)
    {
        var lastAssignmentInstant =
            assignment.AssignmentEndAtUtc is DateTimeOffset endAtUtc && endAtUtc > assignment.AssignmentStartAtUtc
                ? endAtUtc.AddTicks(-1)
                : assignment.AssignmentStartAtUtc;
        var startDate = DateOnly.FromDateTime(
            timeZoneService.ToLocalUnspecified(assignment.AssignmentStartAtUtc, timeZone)
        );
        var endDate = DateOnly.FromDateTime(timeZoneService.ToLocalUnspecified(lastAssignmentInstant, timeZone));
        return timeZoneService.ConvertInclusiveLocalDateRangeToUtcRange(startDate, endDate, timeZone);
    }

    private async Task RemoveOrDelinkAssignmentLinksAsync(
        ShiftEntry shift,
        IReadOnlyCollection<Guid> userIdsToCopy,
        HashSet<int> processedIds,
        bool deleteAssignments,
        CancellationToken cancellationToken
    )
    {
        var assignmentLinks = shift
            .ShiftAssignmentEntries.Where(link => link.Users.Any(user => userIdsToCopy.Contains(user.UserId)))
            .ToList();
        foreach (var assignmentLink in assignmentLinks)
        {
            if (deleteAssignments)
            {
                if (!processedIds.Contains(assignmentLink.AssignmentEntryId))
                {
                    if (
                        !await assignmentEntryServiceFactory()
                            .DeleteAssignmentEntryAsync(assignmentLink.AssignmentEntryId, cancellationToken)
                    )
                        throw new InvalidOperationException(
                            $"Assignment entry {assignmentLink.AssignmentEntryId} linked to loaned-out users could not be deleted."
                        );
                    processedIds.Add(assignmentLink.AssignmentEntryId);
                }
            }
            else if (processedIds.Add(assignmentLink.Id))
            {
                if (
                    !await shiftAssignmentServiceFactory()
                        .DeleteShiftEntryLinkAsync(assignmentLink.Id, cancellationToken)
                )
                    throw new InvalidOperationException(
                        $"Shift-assignment entry link {assignmentLink.Id} could not be removed."
                    );
            }
        }
    }

    private sealed record LoanOutTransferWindow(
        DateTimeOffset StartAtUtc,
        DateTimeOffset EndAtUtc,
        IReadOnlyCollection<Guid> UserIds
    );

    private sealed record ShiftTransfer(ShiftEntry Shift, IReadOnlyCollection<Guid> UserIds);
}
