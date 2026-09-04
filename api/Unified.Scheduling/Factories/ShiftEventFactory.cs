using Microsoft.Extensions.Options;
using Unified.Calendar.Factories;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;
using Unified.Scheduling.Options;
using Unified.Scheduling.Services;

namespace Unified.Scheduling.Factories;

public sealed class ShiftEventFactory(IOptions<WorkingHoursOptions>? workingHoursOptions = null)
    : ICalendarEventEntryFactory<ShiftEntryRequest, ShiftEntry>,
        ICalendarEventSeriesFactory<ShiftSeriesRequest, ShiftSeries>
{
    private readonly WorkingHoursOptions _workingHoursOptions = workingHoursOptions?.Value ?? new();

    public ShiftEntry CreateEntry(ShiftEntryRequest request, Event eventEntity) =>
        new()
        {
            Event = eventEntity,
            ShiftSeriesId = request.ShiftSeriesId,
            LunchAvailableMinutes = request.LunchAvailableMinutes ?? _workingHoursOptions.DefaultLunchMinutes,
            WorkedLunchMinutes = request.WorkedLunchMinutes ?? 0,
            Users = request.UserIds.Distinct().Select(userId => new ShiftEntryUser { UserId = userId }).ToList(),
        };

    public Event GetEvent(ShiftEntry entity) =>
        entity.Event ?? throw new InvalidOperationException($"Shift entry {entity.Id} has no associated event.");

    public ShiftEntry UpdateEntry(ShiftEntryRequest request, ShiftEntry existing)
    {
        existing.ShiftSeriesId = request.ShiftSeriesId;
        existing.LunchAvailableMinutes = request.LunchAvailableMinutes ?? existing.LunchAvailableMinutes;
        existing.WorkedLunchMinutes = request.WorkedLunchMinutes ?? existing.WorkedLunchMinutes;
        SyncUsers(existing, request.UserIds);
        return existing;
    }

    private static void SyncUsers(ShiftEntry entity, IReadOnlyCollection<Guid> requestedUserIds)
    {
        var userIds = requestedUserIds.Distinct().ToHashSet();
        entity.Users = entity.Users.Where(user => userIds.Contains(user.UserId)).ToList();
        var existingUserIds = entity.Users.Select(user => user.UserId).ToHashSet();
        foreach (var userId in userIds.Where(userId => !existingUserIds.Contains(userId)))
            entity.Users.Add(new ShiftEntryUser { UserId = userId });
    }

    public ShiftSeries CreateSeries(ShiftSeriesRequest request, EventSeries eventSeries) =>
        new()
        {
            EventSeries = eventSeries,
            LunchAvailableMinutes = request.LunchAvailableMinutes ?? _workingHoursOptions.DefaultLunchMinutes,
            WorkedLunchMinutes = request.WorkedLunchMinutes,
            Users = request.UserIds.Distinct().Select(userId => new ShiftSeriesUser { UserId = userId }).ToList(),
        };

    public EventSeries GetEventSeries(ShiftSeries entity) =>
        entity.EventSeries
        ?? throw new InvalidOperationException($"Shift series {entity.Id} has no associated event series.");

    // Series relationship synchronization belongs to the feature service, which owns EF tracking.
    public ShiftSeries UpdateSeries(ShiftSeriesRequest request, ShiftSeries existing)
    {
        existing.LunchAvailableMinutes = request.LunchAvailableMinutes ?? existing.LunchAvailableMinutes;
        existing.WorkedLunchMinutes = request.WorkedLunchMinutes;
        return existing;
    }
}
