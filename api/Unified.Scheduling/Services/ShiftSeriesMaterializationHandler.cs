using Microsoft.EntityFrameworkCore;
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

    public async Task OnMaterializedEventsDeletingAsync(
        EventSeries eventSeries,
        IReadOnlyCollection<Event> existingEvents,
        ShiftSeriesMaterializationContext context,
        CancellationToken cancellationToken
    )
    {
        var preservedEventIds = context
            .ExistingEntries.Where(entry =>
                entry.Event is not null
                && entry.Event.LocationId != eventSeries.LocationId
                && existingEvents.Any(eventEntity => eventEntity.Id == entry.EventId)
            )
            .Select(entry => entry.EventId)
            .ToHashSet();
        var existingEventIds = existingEvents
            .Where(eventEntity => !preservedEventIds.Contains(eventEntity.Id))
            .Select(eventEntity => eventEntity.Id)
            .ToHashSet();
        var existingEntries = context.ExistingEntries.Where(entry => existingEventIds.Contains(entry.EventId)).ToList();
        var entryIds = existingEntries.Select(entry => entry.Id).ToList();
        var links = await db
            .ShiftAssignmentEntries.Include(link => link.Users)
            .Where(link => entryIds.Contains(link.ShiftEntryId))
            .ToListAsync(cancellationToken);
        db.ShiftAssignmentEntryUsers.RemoveRange(links.SelectMany(link => link.Users));
        db.ShiftAssignmentEntries.RemoveRange(links);
        db.ShiftEntryUsers.RemoveRange(existingEntries.SelectMany(entry => entry.Users));
        db.ShiftEntries.RemoveRange(existingEntries);
    }

    public Task OnMaterializedEventCreatedAsync(
        EventSeries eventSeries,
        Event eventEntity,
        SeriesEntry occurrence,
        ShiftSeriesMaterializationContext context,
        CancellationToken cancellationToken
    )
    {
        if (
            context.ExistingEntries.Any(entry =>
                entry.Event is not null
                && entry.Event.LocationId != eventSeries.LocationId
                && entry.Event.SeriesStartAtUtc == occurrence.StartAtUtc
            )
        )
        {
            db.Events.Remove(eventEntity);
            return Task.CompletedTask;
        }

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
