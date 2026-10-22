using Unified.Calendar.Models;
using Unified.Common.Calendar.Scheduling.Models;

namespace Unified.Scheduling.Models;

public sealed record AssignmentEntryResponse : CalendarEventEntryResponse
{
    public int? AssignmentSeriesId { get; init; }

    public int AssignmentDefinitionId { get; init; }

    public int CategoryId { get; init; }

    public string? CategoryName { get; init; }

    public int SubCategoryId { get; init; }

    public string? SubCategoryName { get; init; }

    public int Capacity { get; init; }

    public int AssignedUserCount { get; init; }

    public IReadOnlyCollection<int> LinkedShiftEntryIds { get; init; } = [];

    public IReadOnlyCollection<Guid> AssignedUserIds { get; init; } = [];

    public new DateTimeOffset? StartAtUtc { get; init; }

    public int EventId { get; init; }

    public bool IsException { get; init; }

    public IReadOnlyCollection<ShiftAssignmentEntryResponse> AssignmentLinks { get; init; } = [];
}
