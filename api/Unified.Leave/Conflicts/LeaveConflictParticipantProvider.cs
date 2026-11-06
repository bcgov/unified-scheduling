using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Unified.Common.Calendar.Conflicts;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Leave;
using Unified.Leave.Constants;

namespace Unified.Leave.Conflicts;

public sealed class LeaveConflictParticipantProvider(UnifiedDbContext db) : ICalendarConflictParticipantProvider
{
    public static CalendarConflictEventIdentity IdentityFor(int eventId) =>
        CalendarConflictEventIdentity.Create(
            LeaveConstants.SourceModule,
            eventId.ToString(CultureInfo.InvariantCulture)
        );

    public async Task<CalendarConflictParticipant?> GetParticipantAsync(
        CalendarConflictEventIdentity identity,
        Guid resourceId,
        CancellationToken cancellationToken = default
    )
    {
        if (
            identity.SourceModule != LeaveConstants.SourceModule
            || !int.TryParse(identity.EventId, NumberStyles.None, CultureInfo.InvariantCulture, out var eventId)
            || eventId <= 0
            || identity != IdentityFor(eventId)
        )
            return null;

        var participants = await ProjectAsync(
            ActiveUsers().Where(user => user.LeaveEntry!.EventId == eventId && user.UserId == resourceId),
            null,
            cancellationToken
        );
        return participants.SingleOrDefault();
    }

    public async Task<IReadOnlyCollection<CalendarConflictParticipant>> GetParticipantsAsync(
        CalendarConflictQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.EndAtUtc <= query.StartAtUtc)
            return [];

        // Recorded leave has no publish workflow. Its Draft status must not disable publication enforcement.
        // Load across locations; scope only controls whether an otherwise unrelated conflict is displayed.
        var users = ActiveUsers()
            .Where(user =>
                user.LeaveEntry!.Event!.StartAtUtc < query.EndAtUtc
                && user.LeaveEntry.Event.EndAtUtc!.Value > query.StartAtUtc
            );
        if (query.ResourceIds is { Count: > 0 })
        {
            var ids = query.ResourceIds.Distinct().ToList();
            users = users.Where(user => ids.Contains(user.UserId));
        }
        return await ProjectAsync(users, query.LocationId, cancellationToken);
    }

    private IQueryable<LeaveEntryUser> ActiveUsers() =>
        db
            .LeaveEntryUsers.AsNoTracking()
            .Where(user =>
                user.LeaveEntry != null
                && user.LeaveEntry.Event != null
                && user.LeaveEntry.Event.SourceModule == LeaveConstants.SourceModule
                && user.LeaveEntry.Event.EventTypeCode == LeaveConstants.LeaveEventTypeCode
                && user.LeaveEntry.Event.StatusTypeCode != CalendarEventStatusTypeCodes.Cancelled
                && user.LeaveEntry.Event.EndAtUtc.HasValue
                && user.LeaveEntry.Event.EndAtUtc.Value > user.LeaveEntry.Event.StartAtUtc
            );

    private static async Task<IReadOnlyCollection<CalendarConflictParticipant>> ProjectAsync(
        IQueryable<LeaveEntryUser> users,
        int? locationId,
        CancellationToken cancellationToken
    ) => (
            await users
                .Select(user => new CalendarConflictParticipant(
                    user.LeaveEntry!.EventId.ToString(),
                    LeaveConstants.SourceModule,
                    user.UserId,
                    user.LeaveEntry.Event!.StartAtUtc,
                    user.LeaveEntry.Event.EndAtUtc!.Value,
                    // Never expose leave type, title, notes or description through the shared conflict API.
                    "Leave",
                    user.LeaveEntryId,
                    user.LeaveEntry.Event.TimeZoneId,
                    false,
                    !locationId.HasValue
                        || user.LeaveEntry.Event.LocationId == null
                        || user.LeaveEntry.Event.LocationId == locationId,
                    null,
                    user.LeaveEntry.Event.AllDay,
                    null
                ))
                .ToListAsync(cancellationToken)
        ).DistinctBy(participant => (participant.Identity, participant.ResourceId)).ToList();
}
