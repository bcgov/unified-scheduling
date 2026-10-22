using Unified.Calendar.Factories;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Factories;

public sealed class AssignmentEventFactory
    : ICalendarEventEntryFactory<AssignmentEntryRequest, AssignmentEntry>,
        ICalendarEventSeriesFactory<AssignmentSeriesRequest, AssignmentSeries>
{
    public AssignmentEntry CreateEntry(AssignmentEntryRequest request, Event eventEntity) =>
        new()
        {
            AssignmentSeriesId = request.AssignmentSeriesId,
            Event = eventEntity,
            AssignmentDefinitionId = request.AssignmentDefinitionId,
            Capacity = request.Capacity,
            CategoryId = request.CategoryId,
            SubCategoryId = request.SubCategoryId,
        };

    public Event GetEvent(AssignmentEntry entity) =>
        entity.Event ?? throw new InvalidOperationException($"Assignment entry {entity.Id} has no associated event.");

    public AssignmentEntry UpdateEntry(AssignmentEntryRequest request, AssignmentEntry existing)
    {
        existing.AssignmentDefinitionId = request.AssignmentDefinitionId;
        existing.Capacity = request.Capacity;
        existing.CategoryId = request.CategoryId;
        existing.SubCategoryId = request.SubCategoryId;
        return existing;
    }

    public AssignmentEntry UpdateEntry(AssignmentEntryUpdateRequest request, AssignmentEntry existing) =>
        UpdateEntry((AssignmentEntryRequest)request, existing);

    public AssignmentSeries CreateSeries(AssignmentSeriesRequest request, EventSeries eventSeries) =>
        new()
        {
            EventSeries = eventSeries,
            AssignmentDefinitionId = request.AssignmentDefinitionId,
            Capacity = request.Capacity,
            CategoryId = request.CategoryId,
            SubCategoryId = request.SubCategoryId,
        };

    public EventSeries GetEventSeries(AssignmentSeries entity) =>
        entity.EventSeries
        ?? throw new InvalidOperationException($"Assignment series {entity.Id} has no associated event series.");

    public AssignmentSeries UpdateSeries(AssignmentSeriesRequest request, AssignmentSeries existing)
    {
        existing.AssignmentDefinitionId = request.AssignmentDefinitionId;
        existing.Capacity = request.Capacity;
        existing.CategoryId = request.CategoryId;
        existing.SubCategoryId = request.SubCategoryId;
        return existing;
    }
}
