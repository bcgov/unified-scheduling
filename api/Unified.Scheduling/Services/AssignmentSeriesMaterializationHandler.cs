using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;

namespace Unified.Scheduling.Services;

public sealed class AssignmentSeriesMaterializationHandler(UnifiedDbContext db) : IEventSeriesMaterializationHandler
{
    public string SourceModule => SchedulingConstants.SourceModule;

    public string EventTypeCode => SchedulingConstants.AssignmentEventTypeCode;

    public async Task<IEventSeriesMaterializationContext> CreateContextAsync(
        EventSeries eventSeries,
        CancellationToken cancellationToken
    )
    {
        var assignmentSeries =
            await db
                .AssignmentSeries.AsNoTracking()
                .SingleOrDefaultAsync(series => series.EventSeriesId == eventSeries.Id, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Assignment series for event series {eventSeries.Id} was not found."
            );

        return new AssignmentSeriesMaterializationContext
        {
            AssignmentSeriesId = assignmentSeries.Id,
            AssignmentCategoryTypeId = assignmentSeries.AssignmentCategoryTypeId,
            AssignmentSubCategoryTypeId = assignmentSeries.AssignmentSubCategoryTypeId,
            AssignmentTypeId = assignmentSeries.AssignmentTypeId,
            Capacity = assignmentSeries.Capacity,
        };
    }

    public Task<bool> CanRecreateSeriesEntriesAsync(
        EventSeries eventSeries,
        IEnumerable<Event> events,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(
            eventSeries.StatusTypeCode == CalendarEventStatusTypeCodes.Active
                && events.All(e => e.StatusTypeCode == CalendarEventStatusTypeCodes.Active)
        );
    }

    public Task OnEventCreatedAsync(
        EventSeries eventSeries,
        Event eventEntity,
        SeriesEntry occurrence,
        IEventSeriesMaterializationContext context,
        CancellationToken cancellationToken
    )
    {
        var assignmentContext = (AssignmentSeriesMaterializationContext)context;

        db.AssignmentEntries.Add(
            new AssignmentEntry
            {
                AssignmentSeriesId = assignmentContext.AssignmentSeriesId,
                Event = eventEntity,
                AssignmentCategoryTypeId = assignmentContext.AssignmentCategoryTypeId,
                AssignmentSubCategoryTypeId = assignmentContext.AssignmentSubCategoryTypeId,
                AssignmentTypeId = assignmentContext.AssignmentTypeId,
                Capacity = assignmentContext.Capacity,
            }
        );

        return Task.CompletedTask;
    }

    public async Task OnEventRemovedAsync(
        EventSeries eventSeries,
        Event eventEntity,
        IEventSeriesMaterializationContext context,
        CancellationToken cancellationToken
    )
    {
        var assignmentEntry = await db
            .AssignmentEntries.Include(entry => entry.ShiftAssignmentEntries)
            .SingleOrDefaultAsync(entry => entry.EventId == eventEntity.Id, cancellationToken);

        if (assignmentEntry is null)
            return;

        if (assignmentEntry.ShiftAssignmentEntries.Count > 0)
        {
            eventEntity.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;
            eventEntity.CancelledAt = DateTimeOffset.UtcNow;
            eventEntity.CancellationReason = "Removed from assignment series during rematerialization.";
            return;
        }

        db.AssignmentEntries.Remove(assignmentEntry);
    }
}
