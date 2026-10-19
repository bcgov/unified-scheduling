using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Scheduling.Models;

public record AssignmentEntryRequest : CalendarEventEntryRequest
{
    public int AssignmentDefinitionId { get; init; }
    public int? AssignmentSeriesId { get; init; }
    public int CategoryId { get; init; }

    public int SubCategoryId { get; init; }

    public int Capacity { get; init; }

    public DateTimeOffset? SeriesStartAtUtc { get; init; }

    public DateTimeOffset? SeriesEndAtUtc { get; init; }

    public IReadOnlyCollection<ShiftEntryLinkRequest>? ShiftEntryLinks { get; init; }
}
