using Unified.Calendar.Factories;
using Unified.Calendar.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Leave;
using Unified.Leave.Models.Scheduling;

namespace Unified.Leave.Factories;

public sealed class LeaveEventFactory
    : ICalendarEventEntryFactory<SchedulingLeaveEntryRequest, LeaveEntry>,
        ICalendarEventSeriesFactory<SchedulingLeaveSeriesRequest, LeaveSeries>
{
    public LeaveEntry CreateEntry(SchedulingLeaveEntryRequest request, Event eventEntity) =>
        new()
        {
            Event = eventEntity,
            LeaveSeriesId = request.LeaveSeriesId,
            LeaveTypeId = request.LeaveTypeId,
            Users = request.UserIds.Distinct().Select(userId => new LeaveEntryUser { UserId = userId }).ToList(),
        };

    public Event GetEvent(LeaveEntry entity) =>
        entity.Event ?? throw new InvalidOperationException($"Leave entry {entity.Id} has no associated event.");

    public LeaveEntry UpdateEntry(SchedulingLeaveEntryRequest request, LeaveEntry existing)
    {
        existing.LeaveSeriesId = request.LeaveSeriesId;
        existing.LeaveTypeId = request.LeaveTypeId;
        SyncEntryUsers(existing, request.UserIds);
        return existing;
    }

    public static void SyncEntryUsers(LeaveEntry entity, IReadOnlyCollection<Guid> userIds)
    {
        var requestedUserIds = userIds.Distinct().ToHashSet();
        entity.Users = entity.Users.Where(user => requestedUserIds.Contains(user.UserId)).ToList();

        var existingUserIds = entity.Users.Select(user => user.UserId).ToHashSet();
        foreach (var userId in requestedUserIds.Where(userId => !existingUserIds.Contains(userId)))
            entity.Users.Add(new LeaveEntryUser { UserId = userId });
    }

    public LeaveSeries CreateSeries(SchedulingLeaveSeriesRequest request, EventSeries eventSeries) =>
        new()
        {
            EventSeries = eventSeries,
            LeaveTypeId = request.LeaveTypeId,
            Users = request.UserIds.Distinct().Select(userId => new LeaveSeriesUser { UserId = userId }).ToList(),
        };

    public EventSeries GetEventSeries(LeaveSeries entity) =>
        entity.EventSeries
        ?? throw new InvalidOperationException($"Leave series {entity.Id} has no associated event series.");

    public LeaveSeries UpdateSeries(SchedulingLeaveSeriesRequest request, LeaveSeries existing)
    {
        existing.LeaveTypeId = request.LeaveTypeId;
        SyncSeriesUsers(existing, request.UserIds);
        return existing;
    }

    public static void SyncSeriesUsers(LeaveSeries entity, IReadOnlyCollection<Guid> userIds)
    {
        var requestedUserIds = userIds.Distinct().ToHashSet();
        entity.Users = entity.Users.Where(user => requestedUserIds.Contains(user.UserId)).ToList();

        var existingUserIds = entity.Users.Select(user => user.UserId).ToHashSet();
        var createdOn = DateTimeOffset.UtcNow;
        foreach (var userId in requestedUserIds.Where(userId => !existingUserIds.Contains(userId)))
            entity.Users.Add(new LeaveSeriesUser { UserId = userId, CreatedOn = createdOn });
    }
}
