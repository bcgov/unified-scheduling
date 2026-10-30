using Microsoft.EntityFrameworkCore;
using Unified.Authorization;
using Unified.Common.Calendar.Conflicts;
using Unified.Db;
using Unified.Db.Models.Calendar;

namespace Unified.Scheduling.Services;

public sealed class SchedulingConflictParticipantProvider(UnifiedDbContext db) : ICalendarConflictParticipantProvider
{
    public async Task<CalendarConflictParticipant?> GetParticipantAsync(
        CalendarConflictEventIdentity identity,
        Guid resourceId,
        CancellationToken cancellationToken = default
    )
    {
        if (
            identity.SourceModule != SchedulingConstants.SourceModule
            || !int.TryParse(identity.EventId, out var eventId)
        )
            return null;

        var assignmentParticipants = ActiveParticipantQuery(db)
            .Where(participant =>
                participant.ShiftAssignmentEntry!.AssignmentEntry!.EventId == eventId
                && participant.UserId == resourceId
            );
        var assignmentParticipant = (
            await ProjectParticipantsAsync(assignmentParticipants, cancellationToken)
        ).SingleOrDefault();
        if (assignmentParticipant is not null)
            return assignmentParticipant;

        var shiftParticipants = ActiveShiftParticipantQuery(db)
            .Where(participant => participant.ShiftEntry!.EventId == eventId && participant.UserId == resourceId);
        return (await ProjectShiftParticipantsAsync(shiftParticipants, cancellationToken)).SingleOrDefault();
    }

    public async Task<IReadOnlyCollection<CalendarConflictParticipant>> GetParticipantsAsync(
        CalendarConflictQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var canViewAssignments = query.ViewerPermissions?.Contains(nameof(Permissions.AssignmentsView)) == true;
        var canViewShifts = query.ViewerPermissions?.Contains(nameof(Permissions.ShiftsView)) == true;
        if (query.ViewerPermissions is not null && !canViewAssignments && !canViewShifts)
            return [];

        if (canViewShifts && !canViewAssignments)
            return await GetShiftParticipantsAsync(query, cancellationToken);

        var participants = ActiveParticipantQuery(db)
            .Where(participant =>
                participant.ShiftAssignmentEntry!.AssignmentEntry!.Event!.StartAtUtc < query.EndAtUtc
                && participant.ShiftAssignmentEntry.AssignmentEntry.Event.EndAtUtc!.Value > query.StartAtUtc
            );

        if (!query.IncludeDraftParticipants)
        {
            participants = participants.Where(participant =>
                participant.ShiftAssignmentEntry!.AssignmentEntry!.Event!.StatusTypeCode
                    != CalendarEventStatusTypeCodes.Draft
                && participant.ShiftAssignmentEntry.ShiftEntry!.Event!.StatusTypeCode
                    != CalendarEventStatusTypeCodes.Draft
            );
        }

        if (query.ResourceIds is { Count: > 0 })
        {
            var resourceIds = query.ResourceIds.Distinct().ToList();
            participants = participants.Where(participant => resourceIds.Contains(participant.UserId));
        }

        var shifts = await GetShiftParticipantsAsync(query, cancellationToken);
        if (query.LocationId.HasValue)
        {
            var locationId = query.LocationId.Value;
            var scopedResourceIds = await participants
                .Where(participant =>
                    participant.ShiftAssignmentEntry!.AssignmentEntry!.Event!.LocationId == null
                    || participant.ShiftAssignmentEntry.AssignmentEntry.Event.LocationId == locationId
                )
                .Select(participant => participant.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (scopedResourceIds.Count == 0)
                return shifts.Select(AsSupplementalShift).ToList();

            participants = participants.Where(participant => scopedResourceIds.Contains(participant.UserId));
        }

        var assignments = await ProjectParticipantsAsync(participants, cancellationToken, query.LocationId);
        return assignments.Concat(shifts.Select(AsSupplementalShift)).ToList();
    }

    public static Task<IReadOnlyCollection<CalendarConflictParticipant>> GetParticipantsForAssignmentSeriesAsync(
        UnifiedDbContext db,
        int assignmentSeriesId,
        CancellationToken cancellationToken = default
    )
    {
        var participants = PublishedParticipantQuery(db)
            .Where(participant =>
                participant.ShiftAssignmentEntry!.AssignmentEntry!.AssignmentSeriesId == assignmentSeriesId
            );
        return ProjectParticipantsAsync(participants, cancellationToken);
    }

    public static Task<IReadOnlyCollection<CalendarConflictParticipant>> GetParticipantsForAssignmentEntriesAsync(
        UnifiedDbContext db,
        IReadOnlyCollection<int> assignmentEntryIds,
        CancellationToken cancellationToken = default
    )
    {
        if (assignmentEntryIds.Count == 0)
            return Task.FromResult<IReadOnlyCollection<CalendarConflictParticipant>>([]);

        var ids = assignmentEntryIds.Distinct().ToList();
        var participants = PublishedParticipantQuery(db)
            .Where(participant => ids.Contains(participant.ShiftAssignmentEntry!.AssignmentEntryId));
        return ProjectParticipantsAsync(participants, cancellationToken);
    }

    public static async Task<IReadOnlyCollection<CalendarConflictParticipant>> GetParticipantsForShiftEntriesAsync(
        UnifiedDbContext db,
        IReadOnlyCollection<int> shiftEntryIds,
        CancellationToken cancellationToken = default
    )
    {
        if (shiftEntryIds.Count == 0)
            return [];

        var ids = shiftEntryIds.Distinct().ToList();
        var participants = PublishedAssignmentParticipantQuery(db)
            .Where(participant => ids.Contains(participant.ShiftAssignmentEntry!.ShiftEntryId));
        var assignments = await ProjectParticipantsAsync(participants, cancellationToken);
        var shifts = await ProjectShiftParticipantsAsync(
            ActiveShiftParticipantQuery(db)
                .Where(user =>
                    ids.Contains(user.ShiftEntryId)
                    && user.ShiftEntry!.Event!.StatusTypeCode != CalendarEventStatusTypeCodes.Draft
                ),
            cancellationToken
        );
        return assignments.Concat(shifts.Select(AsSupplementalShift)).ToList();
    }

    public static async Task<IReadOnlyCollection<CalendarConflictParticipant>> GetParticipantsForPublicationAsync(
        UnifiedDbContext db,
        IReadOnlyCollection<int> shiftEntryIds,
        IReadOnlyCollection<int> assignmentEntryIds,
        CancellationToken cancellationToken = default
    )
    {
        if (shiftEntryIds.Count == 0 && assignmentEntryIds.Count == 0)
            return [];

        var shiftIds = shiftEntryIds.Distinct().ToList();
        var assignmentIds = assignmentEntryIds.Distinct().ToList();
        var participants = ActiveParticipantQuery(db)
            .Where(participant =>
                shiftIds.Contains(participant.ShiftAssignmentEntry!.ShiftEntryId)
                || (
                    assignmentIds.Contains(participant.ShiftAssignmentEntry.AssignmentEntryId)
                    && participant.ShiftAssignmentEntry.ShiftEntry!.Event!.StatusTypeCode
                        != CalendarEventStatusTypeCodes.Draft
                )
            );
        var assignments = await ProjectParticipantsAsync(participants, cancellationToken);
        var shifts = await ProjectShiftParticipantsAsync(
            ActiveShiftParticipantQuery(db).Where(user => shiftIds.Contains(user.ShiftEntryId)),
            cancellationToken
        );
        return assignments.Concat(shifts.Select(AsSupplementalShift)).ToList();
    }

    // Keep supplemental shift titles generic for viewers who can only view assignments.
    private static CalendarConflictParticipant AsSupplementalShift(CalendarConflictParticipant shift) =>
        shift with
        {
            Title = "Shift",
        };

    private static IQueryable<Unified.Db.Models.Scheduling.ShiftAssignmentEntryUser> ParticipantQuery(
        UnifiedDbContext db
    ) =>
        db
            .ShiftAssignmentEntryUsers.AsNoTracking()
            .Where(participant =>
                participant.ShiftAssignmentEntry != null
                && participant.ShiftAssignmentEntry.AssignmentEntry != null
                && participant.ShiftAssignmentEntry.AssignmentEntry.Event != null
                && participant.ShiftAssignmentEntry.AssignmentEntry.Event.SourceModule
                    == SchedulingConstants.SourceModule
                && participant.ShiftAssignmentEntry.AssignmentEntry.Event.EventTypeCode
                    == SchedulingConstants.AssignmentEventTypeCode
                && participant.ShiftAssignmentEntry.AssignmentEntry.Event.EndAtUtc.HasValue
                && participant.ShiftAssignmentEntry.ShiftEntry != null
                && participant.ShiftAssignmentEntry.ShiftEntry.Event != null
            );

    private static IQueryable<Unified.Db.Models.Scheduling.ShiftAssignmentEntryUser> ActiveParticipantQuery(
        UnifiedDbContext db
    ) =>
        ParticipantQuery(db)
            .Where(participant =>
                participant.ShiftAssignmentEntry!.AssignmentEntry!.Event!.StatusTypeCode
                    != CalendarEventStatusTypeCodes.Cancelled
                && participant.ShiftAssignmentEntry.ShiftEntry!.Event!.StatusTypeCode
                    != CalendarEventStatusTypeCodes.Cancelled
            );

    private static IQueryable<Unified.Db.Models.Scheduling.ShiftAssignmentEntryUser> PublishedAssignmentParticipantQuery(
        UnifiedDbContext db
    ) =>
        ActiveParticipantQuery(db)
            .Where(participant =>
                participant.ShiftAssignmentEntry!.AssignmentEntry!.Event!.StatusTypeCode
                != CalendarEventStatusTypeCodes.Draft
            );

    private static IQueryable<Unified.Db.Models.Scheduling.ShiftEntryUser> ShiftParticipantQuery(UnifiedDbContext db) =>
        db
            .ShiftEntryUsers.AsNoTracking()
            .Where(participant =>
                participant.ShiftEntry != null
                && participant.ShiftEntry.Event != null
                && participant.ShiftEntry.Event.SourceModule == SchedulingConstants.SourceModule
                && participant.ShiftEntry.Event.EventTypeCode == SchedulingConstants.ShiftEventTypeCode
                && participant.ShiftEntry.Event.EndAtUtc.HasValue
            );

    private static IQueryable<Unified.Db.Models.Scheduling.ShiftEntryUser> ActiveShiftParticipantQuery(
        UnifiedDbContext db
    ) =>
        ShiftParticipantQuery(db)
            .Where(participant =>
                participant.ShiftEntry!.Event!.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
            );

    private static IQueryable<Unified.Db.Models.Scheduling.ShiftAssignmentEntryUser> PublishedParticipantQuery(
        UnifiedDbContext db
    ) =>
        PublishedAssignmentParticipantQuery(db)
            .Where(participant =>
                participant.ShiftAssignmentEntry!.ShiftEntry!.Event!.StatusTypeCode
                != CalendarEventStatusTypeCodes.Draft
            );

    private static async Task<IReadOnlyCollection<CalendarConflictParticipant>> ProjectParticipantsAsync(
        IQueryable<Unified.Db.Models.Scheduling.ShiftAssignmentEntryUser> query,
        CancellationToken cancellationToken,
        int? scopeLocationId = null
    )
    {
        var participants = (
            await query
                .Select(participant => new CalendarConflictParticipant(
                    participant.ShiftAssignmentEntry!.AssignmentEntry!.EventId.ToString(),
                    participant.ShiftAssignmentEntry.AssignmentEntry.Event.SourceModule,
                    participant.UserId,
                    participant.ShiftAssignmentEntry.AssignmentEntry.Event.StartAtUtc,
                    participant.ShiftAssignmentEntry.AssignmentEntry.Event.EndAtUtc!.Value,
                    participant.ShiftAssignmentEntry.AssignmentEntry.Event.Title,
                    participant.ShiftAssignmentEntry.AssignmentEntryId,
                    participant.ShiftAssignmentEntry.AssignmentEntry.Event.Location != null
                    && participant.ShiftAssignmentEntry.AssignmentEntry.Event.Location.Timezone.Trim() != ""
                        ? participant.ShiftAssignmentEntry.AssignmentEntry.Event.Location.Timezone
                        : participant.ShiftAssignmentEntry.AssignmentEntry.Event.TimeZoneId,
                    false,
                    !scopeLocationId.HasValue
                        || participant.ShiftAssignmentEntry.AssignmentEntry.Event.LocationId == null
                        || participant.ShiftAssignmentEntry.AssignmentEntry.Event.LocationId == scopeLocationId,
                    null,
                    false,
                    null,
                    participant.ShiftAssignmentEntry.AssignmentEntry.Event.EventTypeCode
                ))
                .ToListAsync(cancellationToken)
        )
            .DistinctBy(participant => (participant.EventId, participant.ResourceId))
            .ToList();
        return participants;
    }

    private static async Task<IReadOnlyCollection<CalendarConflictParticipant>> ProjectShiftParticipantsAsync(
        IQueryable<Unified.Db.Models.Scheduling.ShiftEntryUser> query,
        CancellationToken cancellationToken,
        int? scopeLocationId = null
    )
    {
        var participants = (
            await query
                .Select(participant => new CalendarConflictParticipant(
                    participant.ShiftEntry!.EventId.ToString(),
                    participant.ShiftEntry.Event!.SourceModule,
                    participant.UserId,
                    participant.ShiftEntry.Event.StartAtUtc,
                    participant.ShiftEntry.Event.EndAtUtc!.Value,
                    participant.ShiftEntry.Event.Title,
                    participant.ShiftEntryId,
                    participant.ShiftEntry.Event.Location != null
                    && participant.ShiftEntry.Event.Location.Timezone.Trim() != ""
                        ? participant.ShiftEntry.Event.Location.Timezone
                        : participant.ShiftEntry.Event.TimeZoneId,
                    false,
                    !scopeLocationId.HasValue
                        || participant.ShiftEntry.Event.LocationId == null
                        || participant.ShiftEntry.Event.LocationId == scopeLocationId,
                    null,
                    false,
                    null,
                    participant.ShiftEntry.Event.EventTypeCode
                ))
                .ToListAsync(cancellationToken)
        )
            .DistinctBy(participant => (participant.EventId, participant.ResourceId))
            .ToList();
        return participants;
    }

    private async Task<IReadOnlyCollection<CalendarConflictParticipant>> GetShiftParticipantsAsync(
        CalendarConflictQuery query,
        CancellationToken cancellationToken
    )
    {
        var participants = ActiveShiftParticipantQuery(db)
            .Where(participant =>
                participant.ShiftEntry!.Event!.StartAtUtc < query.EndAtUtc
                && participant.ShiftEntry.Event.EndAtUtc!.Value > query.StartAtUtc
            );

        if (!query.IncludeDraftParticipants)
            participants = participants.Where(participant =>
                participant.ShiftEntry!.Event!.StatusTypeCode != CalendarEventStatusTypeCodes.Draft
            );

        if (query.ResourceIds is { Count: > 0 })
        {
            var resourceIds = query.ResourceIds.Distinct().ToList();
            participants = participants.Where(participant => resourceIds.Contains(participant.UserId));
        }

        if (query.LocationId.HasValue)
        {
            var locationId = query.LocationId.Value;
            var scopedResourceIds = await participants
                .Where(participant =>
                    participant.ShiftEntry!.Event!.LocationId == null
                    || participant.ShiftEntry.Event.LocationId == locationId
                )
                .Select(participant => participant.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (scopedResourceIds.Count == 0)
                return [];

            participants = participants.Where(participant => scopedResourceIds.Contains(participant.UserId));
        }

        return await ProjectShiftParticipantsAsync(participants, cancellationToken, query.LocationId);
    }
}
