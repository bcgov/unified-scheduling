using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Scheduling.Models;

public sealed record AssignmentSeriesResponse : CalendarEventSeriesResponse
{
    public int AssignmentDefinitionId { get; init; }

    public int CategoryId { get; init; }

    public string? CategoryName { get; init; }

    public int SubCategoryId { get; init; }

    public string? SubCategoryName { get; init; }

    public int Capacity { get; init; }

    public IReadOnlyCollection<int> AssignmentEntryIds { get; init; } = [];

    public IReadOnlyCollection<ShiftAssignmentSeriesLinkSummaryResponse> ShiftSeriesLinks { get; init; } = [];
}
