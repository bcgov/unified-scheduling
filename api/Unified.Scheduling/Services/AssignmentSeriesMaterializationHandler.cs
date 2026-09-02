using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;

namespace Unified.Scheduling.Services;

public sealed class AssignmentSeriesMaterializationHandler(UnifiedDbContext db)
    : IEventSeriesMaterializationHandler<AssignmentSeriesMaterializationContext>
{
    public string SourceModule => SchedulingConstants.SourceModule;

    public string EventTypeCode => SchedulingConstants.AssignmentEventTypeCode;

    public Task OnMaterializedEventsDeletingAsync(
        EventSeries eventSeries,
        IReadOnlyCollection<Event> existingEvents,
        AssignmentSeriesMaterializationContext context,
        CancellationToken cancellationToken
    )
    {
        var existingEventIds = existingEvents.Select(eventEntity => eventEntity.Id).ToHashSet();
        var existingEntries = context.ExistingEntries.Where(entry => existingEventIds.Contains(entry.EventId)).ToList();
        db.AssignmentEntries.RemoveRange(existingEntries);
        return Task.CompletedTask;
    }

    public Task OnMaterializedEventCreatedAsync(
        EventSeries eventSeries,
        Event eventEntity,
        SeriesEntry occurrence,
        AssignmentSeriesMaterializationContext context,
        CancellationToken cancellationToken
    )
    {
        db.AssignmentEntries.Add(
            new AssignmentEntry
            {
                AssignmentSeries = context.AssignmentSeries,
                Event = eventEntity,
                AssignmentDefinitionId = context.AssignmentSeries.AssignmentDefinitionId,
                Capacity = context.AssignmentSeries.Capacity,
                CategoryId = context.AssignmentSeries.CategoryId,
                SubCategoryId = context.AssignmentSeries.SubCategoryId,
            }
        );

        return Task.CompletedTask;
    }
}
