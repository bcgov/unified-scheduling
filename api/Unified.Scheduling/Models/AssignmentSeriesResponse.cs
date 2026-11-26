using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Scheduling.Models;

public sealed record AssignmentSeriesResponse : CalendarEventSeriesResponse
{
    public uint ConcurrencyToken { get; init; }

    public Guid? CreatedById { get; init; }

    public DateTimeOffset? CreatedOn { get; init; }

    public Guid? UpdatedById { get; init; }

    public DateTimeOffset? UpdatedOn { get; init; }

    public int AssignmentDefinitionId { get; init; }

    public int CategoryId { get; init; }

    public string? CategoryName { get; init; }

    public int SubCategoryId { get; init; }

    public string? SubCategoryName { get; init; }

    public int Capacity { get; init; }

    public IReadOnlyCollection<int> AssignmentEntryIds { get; init; } = [];

    public IReadOnlyCollection<ShiftAssignmentSeriesLinkSummaryResponse> ShiftSeriesLinks { get; init; } = [];
}
