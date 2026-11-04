using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Calendar;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Mappings;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class LoanOutShiftTransferService(
    ILogger<LoanOutShiftTransferService> logger,
    UnifiedDbContext db,
    SchedulingLoanOutEventMapper eventMapper,
    Func<IShiftService> shiftServiceFactory,
    Func<IShiftAssignmentService> shiftAssignmentServiceFactory,
    ShiftLoanOutAvailabilityService availabilityService,
    ShiftLoanOutExceptionMarkerService markerService,
    IRecurrenceExpander recurrenceExpander
)
{
    public async Task<bool> TransferLoanedOutUserShift(
        LoanOutEntry entry,
        CancellationToken cancellationToken = default
    )
    {
        if (entry.Event is null)
            return false;
        var transferred = await TransferLoanOutEntriesAsync(
            [entry],
            entry.LoanOutLocationId,
            null,
            false,
            entry.Event.StartAtUtc,
            entry.Event.EndAtUtc ?? entry.Event.StartAtUtc,
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
        CancellationToken cancellationToken = default
    )
    {
        if (series.LoanOutEntries.Count == 0)
            return false;
        var eventSeries =
            series.EventSeries
            ?? throw new InvalidOperationException("The loan-out series must include its EventSeries.");
        var lastOccurrence = recurrenceExpander
            .ExpandAllBounded(eventSeries, 400)
            .MaxBy(occurrence => occurrence.StartAtUtc);
        var transferred = await TransferLoanOutEntriesAsync(
            series.LoanOutEntries,
            series.LoanOutLocationId,
            eventSeries.RecurrenceRule,
            true,
            eventSeries.StartAtUtc,
            lastOccurrence?.EndAtUtc ?? eventSeries.EndAtUtc ?? eventSeries.StartAtUtc,
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
        IEnumerable<LoanOutEntry> loanOutEntries,
        int loanOutLocationId,
        string? recurrenceRule,
        bool transferAsSeries,
        DateTimeOffset rangeStartAtUtc,
        DateTimeOffset rangeEndAtUtc,
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

        var userIds = entries.SelectMany(entry => entry.UserIds).Distinct().ToList();

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
        var previousAwayUsers = await availabilityService.GetAwayUserIdsAsync(
            shifts,
            cancellationToken,
            loanOutEntries.Select(entry => entry.Id).ToList()
        );
        var destinationShifts = await db
            .ShiftEntries.AsNoTracking()
            .Include(shift => shift.Event)
            .Include(shift => shift.Users)
            .Where(shift =>
                shift.Event != null
                && shift.Event.LocationId == loanOutLocationId
                && shift.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                && shift.Event.StartAtUtc < rangeEndAtUtc
                && (shift.Event.EndAtUtc ?? shift.Event.StartAtUtc) >= rangeStartAtUtc
                && shift.Users.Any(user => userIds.Contains(user.UserId))
            )
            .ToListAsync(cancellationToken);
        var destinationUsers = destinationShifts
            .GroupBy(shift => (shift.Event!.StartAtUtc, shift.Event.EndAtUtc))
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(shift => shift.Users).Select(user => user.UserId).ToHashSet()
            );
        var matchingShifts = shifts
            .Select(shift =>
            {
                var eventEntity = shift.Event!;
                var copiedUsers = destinationUsers.GetValueOrDefault((eventEntity.StartAtUtc, eventEntity.EndAtUtc));
                var isAway =
                    eventEntity.IsException
                    && eventEntity.ExceptionReason == nameof(CalendarEventExceptionReasonCode.IsAway);
                var shiftUserIds = shift
                    .Users.Where(user => copiedUsers?.Contains(user.UserId) != true)
                    .Select(user => user.UserId)
                    .Where(userId => !isAway || !(previousAwayUsers.GetValueOrDefault(shift) ?? []).Contains(userId))
                    .ToHashSet();
                var matchingUserIds = entries
                    .Where(entry =>
                        ShiftLoanOutAvailabilityService.Overlaps(
                            eventEntity,
                            entry.Event.StartAtUtc,
                            entry.Event.EndAtUtc ?? entry.Event.StartAtUtc
                        )
                    )
                    .SelectMany(entry => entry.UserIds)
                    .Where(shiftUserIds.Contains)
                    .Distinct()
                    .Order()
                    .ToList();
                return new ShiftTransfer(
                    shift,
                    matchingUserIds,
                    (previousAwayUsers.GetValueOrDefault(shift) ?? []).Concat(matchingUserIds).Distinct().ToList()
                );
            })
            .Where(transfer => transfer.UserIds.Count > 0)
            .ToList();
        var transferred = false;

        var recurringTransfers = matchingShifts
            .Where(transfer =>
                transferAsSeries && !string.IsNullOrWhiteSpace(transfer.Shift.ShiftSeries?.EventSeries?.RecurrenceRule)
            )
            .ToList();
        foreach (var transfer in matchingShifts.Except(recurringTransfers))
        {
            await DelinkAssignmentUsersAsync(transfer, cancellationToken);
            var request = eventMapper.ToShiftEntryRequest(transfer.Shift, loanOutLocationId, transfer.UserIds);
            await shiftServiceFactory().CreateShiftEntryAsync(request, cancellationToken);
            markerService.RefreshMarker(transfer.Shift, transfer.AwayUsers);
            transferred = true;
        }

        foreach (
            var group in recurringTransfers.GroupBy(transfer => new
            {
                transfer.Shift.ShiftSeriesId,
                Users = string.Join(",", transfer.UserIds),
            })
        )
        {
            var ordered = group.OrderBy(transfer => transfer.Shift.Event!.StartAtUtc).ToList();
            var candidates = BuildRecurrenceCandidates(ordered, loanOutLocationId, recurrenceRule);
            var runStart = 0;
            var matches = candidates.Select(candidate => candidate.Indexes[0].HasValue).ToArray();
            for (var index = 1; index <= ordered.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var nextMatches =
                    index < ordered.Count
                        ? candidates
                            .Select(
                                (candidate, ruleIndex) =>
                                    matches[ruleIndex]
                                    && candidate.Indexes[index].HasValue
                                    && candidate.Indexes[index] == candidate.Indexes[index - 1] + 1
                            )
                            .ToArray()
                        : [];
                if (nextMatches.Any(match => match))
                {
                    matches = nextMatches;
                    continue;
                }

                var run = ordered.GetRange(runStart, index - runStart);
                var matchingRule = Array.FindIndex(matches, match => match);
                var request =
                    matchingRule >= 0
                        ? BuildSeriesRequest(run, loanOutLocationId, candidates[matchingRule].Rule)
                        : null;
                await CopyRecurringTransfersAsync(run, loanOutLocationId, request, cancellationToken);
                if (index == ordered.Count)
                    break;

                runStart = index;
                matches = candidates.Select(candidate => candidate.Indexes[index].HasValue).ToArray();
                if (!matches.Any(match => match))
                {
                    var remaining = ordered.GetRange(index, ordered.Count - index);
                    var rebased = BuildRecurrenceCandidates(remaining, loanOutLocationId, recurrenceRule);
                    candidates = rebased
                        .Select(candidate =>
                            (
                                candidate.Rule,
                                Indexes: new int?[index]
                                    .Concat(candidate.Indexes)
                                    .ToArray()
                            )
                        )
                        .ToList();
                    matches = candidates.Select(candidate => candidate.Indexes[index].HasValue).ToArray();
                }
            }
            transferred = true;
        }

        if (transferred)
            await db.SaveChangesAsync(cancellationToken);

        return transferred;
    }

    private ShiftSeriesRequest BuildSeriesRequest(
        IReadOnlyCollection<ShiftTransfer> transfers,
        int locationId,
        string? recurrenceRule = null
    ) =>
        eventMapper.ToShiftSeriesRequest(
            transfers.First().Shift.ShiftSeries!,
            transfers.Select(transfer => transfer.Shift).ToList(),
            locationId,
            transfers.First().UserIds,
            recurrenceRule
        );

    private List<(string Rule, int?[] Indexes)> BuildRecurrenceCandidates(
        IReadOnlyList<ShiftTransfer> transfers,
        int locationId,
        string? recurrenceRule
    )
    {
        var sourceRule = transfers[0].Shift.ShiftSeries!.EventSeries!.RecurrenceRule!;
        var rules = new[] { recurrenceRule, sourceRule }
            .Where(rule => !string.IsNullOrWhiteSpace(rule))
            .Select(rule => rule!)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<(string Rule, int?[] Indexes)>();
        foreach (var rule in rules)
        {
            var request = BuildSeriesRequest(transfers, locationId, rule);
            var occurrences = recurrenceExpander.ExpandAllBounded(
                new EventSeries
                {
                    StartAtUtc = request.StartAtUtc,
                    EndAtUtc = request.EndAtUtc,
                    TimeZoneId = request.TimeZoneId,
                    RecurrenceRule = request.RecurrenceRule,
                },
                400
            );
            var occurrenceIndexes = occurrences
                .OrderBy(occurrence => occurrence.StartAtUtc)
                .Select((occurrence, index) => (occurrence, index))
                .ToDictionary(item => (item.occurrence.StartAtUtc, item.occurrence.EndAtUtc), item => item.index);
            var indexes = transfers
                .Select(transfer =>
                    occurrenceIndexes.TryGetValue(
                        (transfer.Shift.Event!.StartAtUtc, transfer.Shift.Event.EndAtUtc),
                        out var index
                    )
                        ? (int?)index
                        : null
                )
                .ToArray();
            candidates.Add((rule, indexes));
        }
        return candidates;
    }

    private async Task CopyRecurringTransfersAsync(
        IReadOnlyCollection<ShiftTransfer> transfers,
        int loanOutLocationId,
        ShiftSeriesRequest? request,
        CancellationToken cancellationToken
    )
    {
        foreach (var transfer in transfers)
            await DelinkAssignmentUsersAsync(transfer, cancellationToken);
        if (request is not null)
            await shiftServiceFactory().CreateShiftSeriesAsync(request, cancellationToken);
        else
        {
            foreach (var transfer in transfers)
                await shiftServiceFactory()
                    .CreateShiftEntryAsync(
                        eventMapper.ToShiftEntryRequest(transfer.Shift, loanOutLocationId, transfer.UserIds),
                        cancellationToken
                    );
        }
        foreach (var transfer in transfers)
            markerService.RefreshMarker(transfer.Shift, transfer.AwayUsers);
    }

    private async Task DelinkAssignmentUsersAsync(ShiftTransfer transfer, CancellationToken cancellationToken) =>
        await DelinkAssignmentUsersAsync(
            transfer.Shift,
            transfer.UserIds,
            shiftAssignmentServiceFactory,
            cancellationToken
        );

    internal static async Task DelinkAssignmentUsersAsync(
        ShiftEntry shift,
        IReadOnlyCollection<Guid> userIds,
        Func<IShiftAssignmentService> assignmentServiceFactory,
        CancellationToken cancellationToken
    )
    {
        foreach (
            var link in shift
                .ShiftAssignmentEntries.Where(link => link.Users.Any(user => userIds.Contains(user.UserId)))
                .ToList()
        )
        {
            var remaining = link
                .Users.Where(user => !userIds.Contains(user.UserId))
                .Select(user => user.UserId)
                .ToList();
            if (remaining.Count > 0)
            {
                var updated = await assignmentServiceFactory()
                    .UpdateShiftEntryLinkAsync(
                        link.Id,
                        new ShiftAssignmentEntryUpdateRequest { UserIds = remaining },
                        cancellationToken
                    );
                if (updated is null)
                    throw new InvalidOperationException($"Shift-assignment entry link {link.Id} could not be updated.");
            }
            else if (!await assignmentServiceFactory().DeleteShiftEntryLinkAsync(link.Id, cancellationToken))
                throw new InvalidOperationException($"Shift-assignment entry link {link.Id} could not be removed.");
        }
    }

    private sealed record ShiftTransfer(
        ShiftEntry Shift,
        IReadOnlyCollection<Guid> UserIds,
        IReadOnlyCollection<Guid> AwayUsers
    );
}
