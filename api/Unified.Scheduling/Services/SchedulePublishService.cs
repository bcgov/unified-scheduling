using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Authorization;
using Unified.Calendar;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Holidays;
using Unified.Calendar.Services;
using Unified.Common.Calendar.Conflicts;
using Unified.Common.Scheduling;
using Unified.Common.Scheduling.Models;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Db.Models.UserManagement;
using Unified.Leave.Services;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

public sealed class SchedulePublishService(
    UnifiedDbContext db,
    ShiftLoanOutAvailabilityService loanOutAvailabilityService,
    ICalendarTimeZoneResolver timeZoneResolver,
    ITimeZoneService timeZoneService,
    IShiftPublicationService shiftPublicationService,
    IAssignmentPublicationService assignmentPublicationService,
    ICalendarConflictService conflictService,
    ILogger<SchedulePublishService> logger,
    ShiftLoanOutAvailabilityService loanOutAvailability,
    StatutoryHolidayCalendarDataProvider holidayDataProvider,
    CalendarConflictProjection conflictProjection,
    ILeaveCalendarDataProvider? leaveDataProvider = null,
    ISchedulingCalendarEventDataProvider<SchedulingCalendarEventResponse>? schedulingCalendarDataProvider = null
) : ISchedulePublishService
{
    public async Task<SchedulePublishPreviewResponse> PreviewAsync(
        SchedulePublishRequest request,
        CancellationToken cancellationToken = default
    ) => (await EvaluateAsync(request, cancellationToken)).Preview;

    public async Task<SchedulePublishResult> PublishAsync(
        SchedulePublishRequest request,
        Guid? userId,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );
        var evaluation = await EvaluateAsync(request, cancellationToken);
        if (!evaluation.Preview.CanPublish)
        {
            logger.LogWarning(
                "Schedule publication blocked for user {UserId}, location {LocationId}, start date {StartDate}, end date {EndDate}, blocker count {BlockingCount}",
                userId,
                request.LocationId,
                request.StartDate,
                request.EndDate,
                evaluation.Preview.Blockers.Conflicts.Count + evaluation.Preview.Blockers.ShiftConflicts.Count
            );
            throw new SchedulePublishBlockedException(evaluation.Preview);
        }

        var shiftResult = await shiftPublicationService.PublishEntriesAsync(
            evaluation.Plan.ShiftEntryIds,
            cancellationToken
        );
        var linkedAssignmentEventIds = shiftResult.PublishedLinkedAssignmentEventIds.ToHashSet();
        var assignmentEntryIds = evaluation
            .Plan.AssignmentCandidates.Where(candidate => !linkedAssignmentEventIds.Contains(candidate.EventId))
            .Select(candidate => candidate.EntryId)
            .ToList();
        var assignmentEntries = await assignmentPublicationService.PublishEntriesAsync(
            assignmentEntryIds,
            cancellationToken
        );
        await transaction.CommitAsync(cancellationToken);

        var result = new SchedulePublishResult(
            shiftResult.ShiftEntries.Count,
            assignmentEntries.Select(entry => entry.EventId).Concat(linkedAssignmentEventIds).Distinct().Count()
        );
        logger.LogInformation(
            "Published schedule for user {UserId}, location {LocationId}, start date {StartDate}, end date {EndDate}: {PublishedShiftEntryCount} shift entries and {PublishedAssignmentEntryCount} assignment entries",
            userId,
            request.LocationId,
            request.StartDate,
            request.EndDate,
            result.PublishedShiftEntryCount,
            result.PublishedAssignmentEntryCount
        );
        return result;
    }

    private async Task<SchedulePublishEvaluation> EvaluateAsync(
        SchedulePublishRequest request,
        CancellationToken cancellationToken
    )
    {
        var scope = await ResolveScopeAsync(request, cancellationToken);
        var snapshot = await LoadScheduleAsync(scope, cancellationToken);
        var plan = await BuildPublicationPlanAsync(snapshot, cancellationToken);
        var findings = await AnalyzePublicationAsync(scope, snapshot, plan, cancellationToken);

        return new SchedulePublishEvaluation(plan, await BuildPreviewAsync(scope, plan, findings, cancellationToken));
    }

    private async Task<SchedulePublishScope> ResolveScopeAsync(
        SchedulePublishRequest request,
        CancellationToken cancellationToken
    )
    {
        var location = await db
            .Locations.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == request.LocationId, cancellationToken);
        if (location is null)
            throw new KeyNotFoundException($"Location {request.LocationId} was not found.");

        var timeZone = timeZoneResolver.Resolve(null, location.Timezone);
        var range = timeZoneService.ConvertInclusiveLocalDateRangeToUtcRange(
            request.StartDate,
            request.EndDate,
            timeZone
        );

        return new SchedulePublishScope(request, timeZone.Id, range);
    }

    private async Task<SchedulePublishSnapshot> LoadScheduleAsync(
        SchedulePublishScope scope,
        CancellationToken cancellationToken
    )
    {
        var shifts = await db
            .ShiftEntries.AsNoTracking()
            .Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .InSchedulingRange(scope.Range)
            .Where(entry => entry.Event.LocationId == scope.Request.LocationId)
            .Where(entry =>
                !loanOutAvailability.IsEnabled
                || !entry.Event.IsException
                || entry.Event.ExceptionReason != nameof(CalendarEventExceptionReasonCode.IsAway)
            )
            .ToListAsync(cancellationToken);
        var assignments = await db
            .AssignmentEntries.AsNoTracking()
            .AsSplitQuery()
            .Include(entry => entry.Event)
            .Include(entry => entry.ShiftAssignmentEntries)
                .ThenInclude(link => link.Users)
            .Include(entry => entry.ShiftAssignmentEntries)
                .ThenInclude(link => link.ShiftEntry!)
                    .ThenInclude(entry => entry.Event)
            .InSchedulingRange(scope.Range)
            .Where(entry => entry.Event.LocationId == scope.Request.LocationId)
            .Where(entry =>
                !loanOutAvailability.IsEnabled
                || !entry.Event.IsException
                || entry.Event.ExceptionReason != nameof(CalendarEventExceptionReasonCode.IsAway)
            )
            .ToListAsync(cancellationToken);

        return new SchedulePublishSnapshot(shifts, assignments);
    }

    private async Task<SchedulePublicationPlan> BuildPublicationPlanAsync(
        SchedulePublishSnapshot snapshot,
        CancellationToken cancellationToken
    )
    {
        var shiftEntryIds = snapshot
            .Shifts.Where(entry => entry.Event!.StatusTypeCode == CalendarEventStatusTypeCodes.Draft)
            .Select(entry => entry.Id)
            .ToList();
        var assignmentCandidates = snapshot
            .Assignments.Where(entry => entry.Event!.StatusTypeCode == CalendarEventStatusTypeCodes.Draft)
            .Select(entry => new AssignmentPublicationCandidate(entry.Id, entry.EventId))
            .ToList();
        var conflictParticipants = await SchedulingConflictParticipantProvider.GetParticipantsForPublicationAsync(
            db,
            loanOutAvailabilityService,
            shiftEntryIds,
            assignmentCandidates.Select(candidate => candidate.EntryId).ToList(),
            cancellationToken
        );

        return new SchedulePublicationPlan(shiftEntryIds, assignmentCandidates, conflictParticipants);
    }

    private async Task<SchedulePublishFindings> AnalyzePublicationAsync(
        SchedulePublishScope scope,
        SchedulePublishSnapshot snapshot,
        SchedulePublicationPlan plan,
        CancellationToken cancellationToken
    )
    {
        var shiftPublicationBlockers = await shiftPublicationService.GetEntryPublicationBlockersAsync(
            plan.ShiftEntryIds,
            cancellationToken
        );
        var conflicts = (
            await conflictService.GetConflictsForCandidatesAsync(plan.ConflictParticipants, cancellationToken)
        )
            .Where(conflict => !conflict.IsOverridden)
            .ToList();

        var unassignedShifts = snapshot
            .Shifts.Where(entry => entry.Users.Count == 0)
            .Select(entry => new SchedulePublishEventWarningResponse(entry.EventId, entry.Event!.Title))
            .ToList();
        var unassignedAssignments = snapshot
            .Assignments.Where(entry =>
                !entry.ShiftAssignmentEntries.Any(link =>
                    link.Users.Count > 0
                    && link.ShiftEntry?.Event?.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                )
            )
            .Select(entry => new SchedulePublishEventWarningResponse(entry.EventId, entry.Event!.Title))
            .ToList();
        var incompleteEmployees = await LoadIncompleteEmployeesAsync(
            scope,
            snapshot.Shifts,
            snapshot.Assignments,
            cancellationToken
        );

        return new SchedulePublishFindings(
            incompleteEmployees,
            unassignedShifts,
            unassignedAssignments,
            conflicts,
            shiftPublicationBlockers
        );
    }

    private async Task<SchedulePublishPreviewResponse> BuildPreviewAsync(
        SchedulePublishScope scope,
        SchedulePublicationPlan plan,
        SchedulePublishFindings findings,
        CancellationToken cancellationToken
    )
    {
        var counts = new SchedulePublishCandidateCountsResponse(
            plan.ShiftEntryIds.Count,
            plan.AssignmentCandidates.Count
        );
        var warnings = new SchedulePublishWarningsResponse(
            findings.IncompleteEmployees,
            conflictProjection.HasPermission(nameof(Permissions.ShiftsView)) ? findings.UnassignedShifts : [],
            conflictProjection.HasPermission(nameof(Permissions.AssignmentsView)) ? findings.UnassignedAssignments : []
        );
        var visible = await conflictProjection.ProjectAsync(findings.Conflicts, cancellationToken);
        var blockers = new SchedulePublishBlockersResponse(
            visible
                .Conflicts.Select(conflict => new SchedulePublishConflictResponse(
                    conflict.Id,
                    conflict.Entry.SourceModule,
                    conflict.Entry.EventId,
                    conflict.Overlaps.SourceModule,
                    conflict.Overlaps.EventId,
                    conflict.ResourceId,
                    conflict.OverlapStart,
                    conflict.OverlapEnd
                ))
                .ToList(),
            findings
                .ShiftPublicationBlockers.Select(blocker => new SchedulePublishShiftBlockerResponse(blocker.Message))
                .ToList()
        )
        {
            RestrictedWarnings = visible.RestrictedWarnings,
            HasRestrictedConflicts = visible.HasRestrictedConflicts,
        };

        return new SchedulePublishPreviewResponse(
            new SchedulePublishScopeResponse(
                scope.Request.LocationId,
                scope.Request.StartDate,
                scope.Request.EndDate,
                scope.TimeZoneId,
                scope.Range.StartAtUtc,
                scope.Range.EndAtUtc
            ),
            counts,
            warnings,
            blockers,
            counts.TotalCount > 0 && findings.Conflicts.Count == 0 && blockers.ShiftConflicts.Count == 0
        );
    }

    private async Task<List<SchedulePublishEmployeeWarningResponse>> LoadIncompleteEmployeesAsync(
        SchedulePublishScope scope,
        IReadOnlyCollection<ShiftEntry> shifts,
        IReadOnlyCollection<AssignmentEntry> assignments,
        CancellationToken cancellationToken
    )
    {
        var employees = await LoadEmployeesForPublicationAsync(scope, cancellationToken);
        var usersWithShifts = shifts.SelectMany(entry => entry.Users).Select(user => user.UserId).ToHashSet();
        var usersWithLinkedAssignments = assignments
            .SelectMany(entry => entry.ShiftAssignmentEntries)
            .Where(link => link.ShiftEntry?.Event?.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled)
            .SelectMany(link => link.Users)
            .Select(user => user.UserId)
            .ToHashSet();

        var incompleteEmployees = employees
            .Where(employee =>
                !usersWithShifts.Contains(employee.UserId) || !usersWithLinkedAssignments.Contains(employee.UserId)
            )
            .ToList();
        if (incompleteEmployees.Count == 0)
            return incompleteEmployees;

        return await FilterUnavailableEmployees(incompleteEmployees, scope, cancellationToken);
    }

    private async Task<List<SchedulePublishEmployeeWarningResponse>> FilterUnavailableEmployees(
        IReadOnlyCollection<SchedulePublishEmployeeWarningResponse> employees,
        SchedulePublishScope scope,
        CancellationToken cancellationToken
    )
    {
        var userIds = employees.Select(employee => employee.UserId).ToList();
        var loanOutEvents = await LoadLoanOutEventsAsync(scope, userIds, cancellationToken);
        var leaveEvents = await LoadLeaveEventsAsync(scope, userIds, cancellationToken);

        return FilterUnavailableEmployees(employees, loanOutEvents.Concat(leaveEvents).ToList(), scope);
    }

    private List<SchedulePublishEmployeeWarningResponse> FilterUnavailableEmployees(
        IReadOnlyCollection<SchedulePublishEmployeeWarningResponse> employees,
        IReadOnlyCollection<UnavailableEvent> unavailableEvents,
        SchedulePublishScope scope
    )
    {
        var unavailableEventsByUser = unavailableEvents
            .GroupBy(participant => participant.UserId)
            .ToDictionary(
                group => group.Key,
                group =>
                    group
                        .Select(participant => new UnavailableEvent(
                            participant.UserId,
                            participant.Type,
                            participant.StartAtUtc,
                            participant.EndAtUtc
                        ))
                        .ToList()
            );
        var timeZone = timeZoneService.ResolveRequired(scope.TimeZoneId);

        return employees
            .Where(employee =>
                !unavailableEventsByUser.TryGetValue(employee.UserId, out var events)
                || !HasEventsForAllWeekdays(events, scope.Request.StartDate, scope.Request.EndDate, timeZone)
            )
            .ToList();
    }

    private async Task<List<UnavailableEvent>> LoadLoanOutEventsAsync(
        SchedulePublishScope scope,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken
    )
    {
        if (!loanOutAvailability.IsEnabled || schedulingCalendarDataProvider is null)
            return [];

        var calendarEvents = await schedulingCalendarDataProvider.GetDataAsync(
            new SchedulingCalendarEventQueryContext
            {
                StartAtUtc = scope.Range.StartAtUtc,
                EndAtUtc = scope.Range.EndAtUtc,
                LocationId = scope.Request.LocationId,
                UserIds = userIds,
            },
            cancellationToken
        );

        return calendarEvents
            .Where(calendarEvent => calendarEvent.LoanOutLocationId != scope.Request.LocationId)
            .SelectMany(calendarEvent =>
                calendarEvent
                    .UserIds.Where(userIds.Contains)
                    .Select(userId => new UnavailableEvent(
                        userId,
                        calendarEvent.Type,
                        calendarEvent.Start,
                        calendarEvent.End
                    ))
            )
            .ToList();
    }

    private async Task<List<Guid>> LoadLoanedInUserIdsAsync(
        SchedulePublishScope scope,
        CancellationToken cancellationToken
    )
    {
        if (!loanOutAvailability.IsEnabled || schedulingCalendarDataProvider is null)
            return [];

        var calendarEvents = await schedulingCalendarDataProvider.GetDataAsync(
            new SchedulingCalendarEventQueryContext
            {
                StartAtUtc = scope.Range.StartAtUtc,
                EndAtUtc = scope.Range.EndAtUtc,
            },
            cancellationToken
        );

        return calendarEvents
            .Where(calendarEvent => calendarEvent.LoanOutLocationId == scope.Request.LocationId)
            .SelectMany(calendarEvent => calendarEvent.UserIds)
            .Distinct()
            .ToList();
    }

    private async Task<List<UnavailableEvent>> LoadLeaveEventsAsync(
        SchedulePublishScope scope,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken
    )
    {
        if (leaveDataProvider is null)
            return [];

        var leaveEvents = await leaveDataProvider.GetEventsAsync(
            new CalendarEventQueryContext
            {
                StartAtUtc = scope.Range.StartAtUtc,
                EndAtUtc = scope.Range.EndAtUtc,
                LocationId = scope.Request.LocationId,
                UserIds = userIds,
            },
            cancellationToken
        );

        return leaveEvents
            .SelectMany(calendarEvent =>
                calendarEvent
                    .UserIds.Where(userIds.Contains)
                    .Select(userId => new UnavailableEvent(
                        userId,
                        calendarEvent.Type,
                        calendarEvent.Start,
                        calendarEvent.End
                    ))
            )
            .ToList();
    }

    private async Task<List<SchedulePublishEmployeeWarningResponse>> LoadEmployeesForPublicationAsync(
        SchedulePublishScope scope,
        CancellationToken cancellationToken
    )
    {
        var loanedInUserIds = await LoadLoanedInUserIdsAsync(scope, cancellationToken);
        var employees = await db
            .Users.AsNoTracking()
            .Where(user =>
                user.IsEnabled
                && (
                    user.HomeLocationId == scope.Request.LocationId
                    || user.UserLocations.Any(location => location.LocationId == scope.Request.LocationId)
                    || loanedInUserIds.Contains(user.Id)
                )
            )
            .Select(user => new User
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                HomeLocationId = user.HomeLocationId,
                UserLocations = user
                    .UserLocations.Where(location => location.LocationId == scope.Request.LocationId)
                    .Select(location => new UserLocation
                    {
                        LocationId = location.LocationId,
                        EffectiveFrom = location.EffectiveFrom,
                        EffectiveUntil = location.EffectiveUntil,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        var timeZone = timeZoneService.ResolveRequired(scope.TimeZoneId);
        return employees
            .Where(employee =>
                loanedInUserIds.Contains(employee.Id) || IsEmployeeAtLocationForPublication(employee, scope, timeZone)
            )
            .Select(employee => new SchedulePublishEmployeeWarningResponse(
                employee.Id,
                $"{employee.FirstName} {employee.LastName}".Trim()
            ))
            .ToList();
    }

    private bool IsEmployeeAtLocationForPublication(User user, SchedulePublishScope scope, TimeZoneInfo timeZone)
    {
        DateOnly LocalDate(DateTimeOffset value) =>
            DateOnly.FromDateTime(timeZoneService.ToLocalUnspecified(value, timeZone));

        return user.HomeLocationId == scope.Request.LocationId
            || user.UserLocations.Any(location =>
                location.LocationId == scope.Request.LocationId
                && (
                    !location.EffectiveFrom.HasValue || LocalDate(location.EffectiveFrom.Value) <= scope.Request.EndDate
                )
                && LocalDate(location.EffectiveUntil) > scope.Request.StartDate
            );
    }

    private bool HasEventsForAllWeekdays(
        IReadOnlyCollection<UnavailableEvent> events,
        DateOnly startDate,
        DateOnly endDate,
        TimeZoneInfo timeZone
    )
    {
        if (endDate < startDate)
            throw new ArgumentOutOfRangeException(nameof(endDate), "End date must be on or after start date.");

        var holidayDates = holidayDataProvider
            .GetEvents(startDate, endDate, timeZone)
            .Select(holiday => DateOnly.FromDateTime(timeZoneService.ToLocalUnspecified(holiday.StartAtUtc, timeZone)))
            .ToHashSet();

        for (var dayNumber = startDate.DayNumber; dayNumber <= endDate.DayNumber; dayNumber++)
        {
            var date = DateOnly.FromDayNumber(dayNumber);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || holidayDates.Contains(date))
                continue;

            var range = timeZoneService.ConvertInclusiveLocalDateRangeToUtcRange(date, date, timeZone);
            if (
                !events.Any(calendarEvent =>
                    calendarEvent.StartAtUtc < range.EndAtUtc
                    && (
                        calendarEvent.EndAtUtc.HasValue
                            ? calendarEvent.EndAtUtc > range.StartAtUtc
                            : calendarEvent.StartAtUtc >= range.StartAtUtc
                    )
                )
            )
                return false;
        }
        return true;
    }

    private sealed record UnavailableEvent(
        Guid UserId,
        string Type,
        DateTimeOffset StartAtUtc,
        DateTimeOffset? EndAtUtc
    );

    private sealed record SchedulePublishScope(SchedulePublishRequest Request, string TimeZoneId, UtcDateRange Range);

    private sealed record SchedulePublishSnapshot(
        IReadOnlyCollection<ShiftEntry> Shifts,
        IReadOnlyCollection<AssignmentEntry> Assignments
    );

    private sealed record AssignmentPublicationCandidate(int EntryId, int EventId);

    private sealed record SchedulePublicationPlan(
        IReadOnlyCollection<int> ShiftEntryIds,
        IReadOnlyCollection<AssignmentPublicationCandidate> AssignmentCandidates,
        IReadOnlyCollection<CalendarConflictParticipant> ConflictParticipants
    );

    private sealed record SchedulePublishFindings(
        IReadOnlyCollection<SchedulePublishEmployeeWarningResponse> IncompleteEmployees,
        IReadOnlyCollection<SchedulePublishEventWarningResponse> UnassignedShifts,
        IReadOnlyCollection<SchedulePublishEventWarningResponse> UnassignedAssignments,
        IReadOnlyCollection<CalendarConflict> Conflicts,
        IReadOnlyCollection<ShiftPublicationBlocker> ShiftPublicationBlockers
    );

    private sealed record SchedulePublishEvaluation(
        SchedulePublicationPlan Plan,
        SchedulePublishPreviewResponse Preview
    );
}
