using Unified.Calendar.Services;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Leave;
using Unified.Leave.Models;

namespace Unified.Leave.Services;

/// <summary>
/// Series-level operations for leave: materialization/regeneration through the shared
/// <see cref="IEventSeriesMaterializationService"/> pipeline using the
/// <see cref="LeaveSeriesMaterializationHandler"/>, and override-preserving field propagation.
/// Injected directly into <see cref="SchedulingLeaveService"/>.
/// </summary>
public sealed class SchedulingLeaveSeriesFacade(
    IEventSeriesMaterializationService materializer,
    LeaveSeriesMaterializationHandler handler
)
{
    public RecurrenceValidationOptions RecurrenceOptions { get; } =
        new()
        {
            MaximumDuration = TimeSpan.FromDays(365),
            MaximumOccurrences = 400,
            RequireBoundedRule = true,
        };

    /// <summary>Checks loaded occurrences before the tracked series is modified.</summary>
    public void EnsureCanRegenerate(IEnumerable<LeaveEntry> entries)
    {
        if (entries.Any(entry => entry.Event?.StatusTypeCode != CalendarEventStatusTypeCodes.Draft))
        {
            throw new InvalidOperationException("Materialized leave entries cannot be recreated in the current state.");
        }
    }

    public async Task MaterializeSeriesAsync(
        LeaveSeries entity,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var eventSeries =
            entity.EventSeries ?? throw new InvalidOperationException("Leave series has no calendar series.");

        var context = new LeaveSeriesMaterializationContext { LeaveSeries = entity, UserIds = userIds };
        await materializer.MaterializeAsync(eventSeries, RecurrenceOptions, handler, context, cancellationToken);
    }

    public async Task RegenerateSeriesAsync(
        LeaveSeries entity,
        IReadOnlyCollection<Guid> userIds,
        IReadOnlyCollection<LeaveEntry> existingEntries,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(userIds);
        ArgumentNullException.ThrowIfNull(existingEntries);

        var eventSeries =
            entity.EventSeries ?? throw new InvalidOperationException("Leave series has no calendar series.");

        var context = new LeaveSeriesMaterializationContext
        {
            LeaveSeries = entity,
            UserIds = userIds,
            ExistingEntries = existingEntries,
        };
        await materializer.RegenerateDraftSeriesAsync(
            eventSeries,
            RecurrenceOptions,
            handler,
            context,
            cancellationToken
        );
    }

    public LeaveEventCopiedValues CaptureCopiedValues(EventSeries eventSeries)
    {
        ArgumentNullException.ThrowIfNull(eventSeries);

        return new LeaveEventCopiedValues(
            eventSeries.Title,
            eventSeries.Description,
            eventSeries.Notes,
            eventSeries.Color,
            eventSeries.LocationId
        );
    }

    public void ApplyCopiedFieldUpdatesPreservingOverrides(
        Event eventEntity,
        LeaveEventCopiedValues oldValues,
        LeaveEventCopiedValues newValues
    )
    {
        ArgumentNullException.ThrowIfNull(eventEntity);
        ArgumentNullException.ThrowIfNull(oldValues);
        ArgumentNullException.ThrowIfNull(newValues);

        if (eventEntity.Title == oldValues.Title)
            eventEntity.Title = newValues.Title;
        if (eventEntity.Description == oldValues.Description)
            eventEntity.Description = newValues.Description;
        if (eventEntity.Notes == oldValues.Notes)
            eventEntity.Notes = newValues.Notes;
        if (eventEntity.Color == oldValues.Color)
            eventEntity.Color = newValues.Color;
        if (eventEntity.LocationId == oldValues.LocationId)
            eventEntity.LocationId = newValues.LocationId;
    }

    public sealed record LeaveEventCopiedValues(
        string Title,
        string? Description,
        string? Notes,
        string? Color,
        int? LocationId
    );
}
