using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

internal static class AssignmentSeriesUpdatePlanner
{
    public static AssignmentSeriesUpdatePlan CreatePlan(
        AssignmentSeries assignmentSeries,
        AssignmentSeriesRequest request
    )
    {
        var eventSeries = assignmentSeries.EventSeries!;
        var recurrenceChanged =
            !StringEqualsNormalized(eventSeries.RecurrenceRule, request.RecurrenceRule)
            || eventSeries.StartAtUtc != request.StartAtUtc
            || eventSeries.EndAtUtc != request.EndAtUtc
            || !StringEqualsNormalized(eventSeries.TimeZoneId, request.TimeZoneId)
            || eventSeries.AllDay != request.AllDay;

        return new AssignmentSeriesUpdatePlan(
            RecurrenceChanged: recurrenceChanged,
            RegenerateEntries: recurrenceChanged,
            PropagateSeriesChanges: !recurrenceChanged,
            PreviousValues: new AssignmentSeriesPreviousValues(
                eventSeries.Title,
                eventSeries.Description,
                eventSeries.Notes,
                eventSeries.Color,
                eventSeries.LocationId,
                assignmentSeries.AssignmentDefinitionId,
                assignmentSeries.Capacity,
                assignmentSeries.CategoryId,
                assignmentSeries.SubCategoryId
            )
        );
    }

    private static bool StringEqualsNormalized(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.Ordinal);
}

internal sealed record AssignmentSeriesUpdatePlan(
    bool RecurrenceChanged,
    bool RegenerateEntries,
    bool PropagateSeriesChanges,
    AssignmentSeriesPreviousValues PreviousValues
);

internal sealed record AssignmentSeriesPreviousValues(
    string Title,
    string? Description,
    string? Notes,
    string? Color,
    int? LocationId,
    int AssignmentDefinitionId,
    int Capacity,
    int CategoryId,
    int SubCategoryId
);
