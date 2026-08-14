using Unified.Db.Models.Calendar;
using Unified.Scheduling.Models;

namespace Unified.Scheduling.Services;

internal static class ShiftSeriesUpdatePlanner
{
    public static bool HasRecurrenceChanged(EventSeries eventSeries, ShiftSeriesRequest request) =>
        !StringEqualsNormalized(eventSeries.RecurrenceRule, request.RecurrenceRule)
        || eventSeries.StartAtUtc != request.StartAtUtc
        || eventSeries.EndAtUtc != request.EndAtUtc
        || !StringEqualsNormalized(eventSeries.TimeZoneId, request.TimeZoneId)
        || eventSeries.AllDay != request.AllDay;

    public static EventSeriesCopiedValues CaptureCopiedValues(EventSeries eventSeries) =>
        new(eventSeries.Title, eventSeries.Description, eventSeries.Notes, eventSeries.Color, eventSeries.LocationId);

    public static void ApplyCopiedFieldUpdatesPreservingOverrides(
        Event eventEntity,
        EventSeriesCopiedValues oldValues,
        EventSeries updatedSeries
    )
    {
        if (eventEntity.Title == oldValues.Title)
            eventEntity.Title = updatedSeries.Title;
        if (eventEntity.Description == oldValues.Description)
            eventEntity.Description = updatedSeries.Description;
        if (eventEntity.Notes == oldValues.Notes)
            eventEntity.Notes = updatedSeries.Notes;
        if (eventEntity.Color == oldValues.Color)
            eventEntity.Color = updatedSeries.Color;
        if (eventEntity.LocationId == oldValues.LocationId)
            eventEntity.LocationId = updatedSeries.LocationId;
    }

    private static bool StringEqualsNormalized(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.Ordinal);
}

internal sealed record EventSeriesCopiedValues(
    string Title,
    string? Description,
    string? Notes,
    string? Color,
    int? LocationId
);
