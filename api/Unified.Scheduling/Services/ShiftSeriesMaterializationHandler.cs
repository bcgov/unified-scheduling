using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;

namespace Unified.Scheduling.Services;

public sealed class ShiftSeriesMaterializationHandler(UnifiedDbContext db)
    : IEventSeriesMaterializationHandler<ShiftSeriesMaterializationContext>
{
    public string SourceModule => SchedulingConstants.SourceModule;

    public string EventTypeCode => SchedulingConstants.ShiftEventTypeCode;

    public Task OnMaterializedEventsDeletingAsync(
        EventSeries eventSeries,
        IReadOnlyCollection<Event> existingEvents,
        ShiftSeriesMaterializationContext context,
        CancellationToken cancellationToken
    )
    {
        var existingEventIds = existingEvents.Select(eventEntity => eventEntity.Id).ToHashSet();
        var existingEntries = context.ExistingEntries.Where(entry => existingEventIds.Contains(entry.EventId)).ToList();
        db.ShiftEntryUsers.RemoveRange(existingEntries.SelectMany(entry => entry.Users));
        db.ShiftEntries.RemoveRange(existingEntries);
        return Task.CompletedTask;
    }

    public Task OnMaterializedEventCreatedAsync(
        EventSeries eventSeries,
        Event eventEntity,
        SeriesEntry occurrence,
        ShiftSeriesMaterializationContext context,
        CancellationToken cancellationToken
    )
    {
        db.ShiftEntries.Add(
            new ShiftEntry
            {
                ShiftSeries = context.ShiftSeries,
                Event = eventEntity,
                Users = context.UserIds.Select(userId => new ShiftEntryUser { UserId = userId }).ToList(),
                LunchAvailableMinutes = context.ShiftSeries.LunchAvailableMinutes,
                WorkedLunchMinutes = context.ShiftSeries.WorkedLunchMinutes,
            }
        );

        return Task.CompletedTask;
    }
}
