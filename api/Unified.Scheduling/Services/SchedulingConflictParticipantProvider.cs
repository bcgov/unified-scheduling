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
        var entries = db
            .AssignmentEntries.AsNoTracking()
            .Include(entry => entry.Event)
            .Include(entry => entry.ShiftAssignmentEntries)
                .ThenInclude(link => link.Users)
            .Include(entry => entry.ShiftAssignmentEntries)
                .ThenInclude(link => link.ShiftEntry!)
                    .ThenInclude(shiftEntry => shiftEntry.Event)
            .Where(entry =>
                entry.Event != null
                && entry.Event.SourceModule == SchedulingConstants.SourceModule
                && entry.Event.EventTypeCode == SchedulingConstants.AssignmentEventTypeCode
                && entry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                && entry.Event.EndAtUtc.HasValue
                && entry.Event.StartAtUtc < query.EndAtUtc
                && entry.Event.EndAtUtc.Value > query.StartAtUtc
            );

        if (query.ExcludedEventIds is { Count: > 0 })
        {
            var excludedEventIds = query.ExcludedEventIds.Distinct().ToList();
            entries = entries.Where(entry => !excludedEventIds.Contains(entry.EventId));
        }

        if (query.ResourceIds is { Count: > 0 })
        {
            var resourceIds = query.ResourceIds.Distinct().ToList();
            entries = entries.Where(entry =>
                entry.ShiftAssignmentEntries.Any(link =>
                    link.ShiftEntry != null
                    && link.ShiftEntry.Event != null
                    && link.ShiftEntry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                    && link.Users.Any(user => resourceIds.Contains(user.UserId))
                )
            );
        }

        var loadedEntries = await entries.ToListAsync(cancellationToken);
        return loadedEntries
            .SelectMany(entry =>
                entry
                    .ShiftAssignmentEntries.Where(link =>
                        link.ShiftEntry?.Event?.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                    )
                    .SelectMany(link => link.Users)
                    .Select(user => user.UserId)
                    .Distinct()
                    .Select(userId => new CalendarConflictParticipant(
                        entry.EventId,
                        entry.Event!.EventTypeCode,
                        entry.Event.SourceModule,
                        userId,
                        entry.Event.StartAtUtc,
                        entry.Event.EndAtUtc!.Value,
                        entry.Event.Title
                    ))
            )
            .ToList();
    }
}
