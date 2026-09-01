using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Conflicts;
using Unified.Db;
using Unified.Db.Models.Calendar;

namespace Unified.Scheduling.Services;

public sealed class SchedulingConflictParticipantProvider(UnifiedDbContext db) : ICalendarConflictParticipantProvider
{
    public async Task<IReadOnlyCollection<CalendarConflictParticipant>> GetParticipantsAsync(
        CalendarConflictQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var participants = ParticipantQuery(db)
            .Where(participant =>
                participant.ShiftAssignmentEntry!.AssignmentEntry!.Event!.StatusTypeCode
                    != CalendarEventStatusTypeCodes.Cancelled
                && participant.ShiftAssignmentEntry.ShiftEntry!.Event!.StatusTypeCode
                    != CalendarEventStatusTypeCodes.Cancelled
                && participant.ShiftAssignmentEntry.AssignmentEntry.Event.StartAtUtc < query.EndAtUtc
                && participant.ShiftAssignmentEntry.AssignmentEntry.Event.EndAtUtc!.Value > query.StartAtUtc
            );

        if (query.ExcludedEventIds is { Count: > 0 })
        {
            var excludedEventIds = query.ExcludedEventIds.Distinct().ToList();
            participants = participants.Where(participant =>
                !excludedEventIds.Contains(participant.ShiftAssignmentEntry!.AssignmentEntry!.EventId)
            );
        }

        if (query.ResourceIds is { Count: > 0 })
        {
            var resourceIds = query.ResourceIds.Distinct().ToList();
            participants = participants.Where(participant => resourceIds.Contains(participant.UserId));
        }

        return await ProjectParticipantsAsync(participants, cancellationToken);
    }

    public static async Task<IReadOnlyCollection<CalendarConflictParticipant>> GetParticipantsForAssignmentSeriesAsync(
        UnifiedDbContext db,
        int assignmentSeriesId,
        CancellationToken cancellationToken = default
    )
    {
        var entryIds = await db
            .AssignmentEntries.Where(entry => entry.AssignmentSeriesId == assignmentSeriesId)
            .Select(entry => entry.Id)
            .ToListAsync(cancellationToken);
        return await GetParticipantsForAssignmentEntriesAsync(db, entryIds, cancellationToken);
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
        var participants = ParticipantQuery(db)
            .Where(participant =>
                ids.Contains(participant.ShiftAssignmentEntry!.AssignmentEntryId)
                && participant.ShiftAssignmentEntry.AssignmentEntry!.Event!.StatusTypeCode
                    != CalendarEventStatusTypeCodes.Draft
                && participant.ShiftAssignmentEntry.AssignmentEntry.Event.StatusTypeCode
                    != CalendarEventStatusTypeCodes.Cancelled
                && participant.ShiftAssignmentEntry.ShiftEntry!.Event!.StatusTypeCode
                    != CalendarEventStatusTypeCodes.Draft
                && participant.ShiftAssignmentEntry.ShiftEntry.Event.StatusTypeCode
                    != CalendarEventStatusTypeCodes.Cancelled
            );
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
        var participants = ParticipantQuery(db)
            .Where(participant =>
                ids.Contains(participant.ShiftAssignmentEntry!.ShiftEntryId)
                && participant.ShiftAssignmentEntry.AssignmentEntry!.Event!.StatusTypeCode
                    != CalendarEventStatusTypeCodes.Cancelled
                && participant.ShiftAssignmentEntry.ShiftEntry!.Event!.StatusTypeCode
                    != CalendarEventStatusTypeCodes.Cancelled
            );
        return await ProjectParticipantsAsync(participants, cancellationToken);
    }

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

    private static async Task<IReadOnlyCollection<CalendarConflictParticipant>> ProjectParticipantsAsync(
        IQueryable<Unified.Db.Models.Scheduling.ShiftAssignmentEntryUser> query,
        CancellationToken cancellationToken
    ) =>
        (
            await query
                .Select(participant => new CalendarConflictParticipant(
                    participant.ShiftAssignmentEntry!.AssignmentEntry!.EventId,
                    participant.ShiftAssignmentEntry.AssignmentEntry.Event!.EventTypeCode,
                    participant.ShiftAssignmentEntry.AssignmentEntry.Event.SourceModule,
                    participant.UserId,
                    participant.ShiftAssignmentEntry.AssignmentEntry.Event.StartAtUtc,
                    participant.ShiftAssignmentEntry.AssignmentEntry.Event.EndAtUtc!.Value,
                    participant.ShiftAssignmentEntry.AssignmentEntry.Event.Title,
                    participant.ShiftAssignmentEntry.AssignmentEntryId,
                    participant.ShiftAssignmentEntry.AssignmentEntry.Event.TimeZoneId
                ))
                .ToListAsync(cancellationToken)
        )
            .DistinctBy(participant => (participant.EventId, participant.ResourceId))
            .ToList();
}
