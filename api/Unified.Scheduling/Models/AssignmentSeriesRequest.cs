using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Scheduling.Models;

public sealed record AssignmentSeriesRequest : CalendarEventSeriesRequest
{
    public int AssignmentDefinitionId { get; init; }
    public int CategoryId { get; init; }

    public int SubCategoryId { get; init; }

    public int Capacity { get; init; }

    public IReadOnlyCollection<ShiftSeriesLinkRequest> ShiftSeriesLinks { get; init; } = [];
}
