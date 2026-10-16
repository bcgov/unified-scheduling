using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.TimeOff;
using Unified.TimeOff.Constants;
using Unified.TimeOff.Models;

namespace Unified.TimeOff.Services;

public sealed class TimeOffSeriesMaterializationHandler(UnifiedDbContext db)
    : IEventSeriesMaterializationHandler<TimeOffSeriesMaterializationContext>
{
    public string SourceModule => TimeOffConstants.SourceModule;

    public string EventTypeCode => TimeOffConstants.TimeOffEventTypeCode;

    public Task OnMaterializedEventsDeletingAsync(
        EventSeries eventSeries,
        IReadOnlyCollection<Event> existingEvents,
        TimeOffSeriesMaterializationContext context,
        CancellationToken cancellationToken
    )
    {
        var existingEventIds = existingEvents.Select(eventEntity => eventEntity.Id).ToHashSet();
        var existingEntries = context.ExistingEntries.Where(entry => existingEventIds.Contains(entry.EventId)).ToList();
        db.Set<TimeOffEntryUser>().RemoveRange(existingEntries.SelectMany(entry => entry.Users));
        db.TimeOffEntries.RemoveRange(existingEntries);
        return Task.CompletedTask;
    }

    public Task OnMaterializedEventCreatedAsync(
        EventSeries eventSeries,
        Event eventEntity,
        SeriesEntry occurrence,
        TimeOffSeriesMaterializationContext context,
        CancellationToken cancellationToken
    )
    {
        db.TimeOffEntries.Add(
            new TimeOffEntry
            {
                TimeOffSeries = context.TimeOffSeries,
                LeaveTypeId = context.TimeOffSeries.LeaveTypeId,
                Event = eventEntity,
                Users = context.UserIds.Select(userId => new TimeOffEntryUser { UserId = userId }).ToList(),
            }
        );

        return Task.CompletedTask;
    }
}
