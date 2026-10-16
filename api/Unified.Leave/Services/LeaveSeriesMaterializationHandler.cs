using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Leave;
using Unified.Leave.Constants;
using Unified.Leave.Models;

namespace Unified.Leave.Services;

public sealed class LeaveSeriesMaterializationHandler(UnifiedDbContext db)
    : IEventSeriesMaterializationHandler<LeaveSeriesMaterializationContext>
{
    public string SourceModule => LeaveConstants.SourceModule;

    public string EventTypeCode => LeaveConstants.LeaveEventTypeCode;

    public Task OnMaterializedEventsDeletingAsync(
        EventSeries eventSeries,
        IReadOnlyCollection<Event> existingEvents,
        LeaveSeriesMaterializationContext context,
        CancellationToken cancellationToken
    )
    {
        var existingEventIds = existingEvents.Select(eventEntity => eventEntity.Id).ToHashSet();
        var existingEntries = context.ExistingEntries.Where(entry => existingEventIds.Contains(entry.EventId)).ToList();
        db.Set<LeaveEntryUser>().RemoveRange(existingEntries.SelectMany(entry => entry.Users));
        db.LeaveEntries.RemoveRange(existingEntries);
        return Task.CompletedTask;
    }

    public Task OnMaterializedEventCreatedAsync(
        EventSeries eventSeries,
        Event eventEntity,
        SeriesEntry occurrence,
        LeaveSeriesMaterializationContext context,
        CancellationToken cancellationToken
    )
    {
        db.LeaveEntries.Add(
            new LeaveEntry
            {
                LeaveSeries = context.LeaveSeries,
                LeaveTypeId = context.LeaveSeries.LeaveTypeId,
                Event = eventEntity,
                Users = context.UserIds.Select(userId => new LeaveEntryUser { UserId = userId }).ToList(),
            }
        );

        return Task.CompletedTask;
    }
}
