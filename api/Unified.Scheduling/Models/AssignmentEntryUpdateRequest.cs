namespace Unified.Scheduling.Models;

public sealed record AssignmentEntryUpdateRequest
{
    public int AssignmentDefinitionId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? Notes { get; init; }

    public string Color { get; init; } = string.Empty;

    public DateTimeOffset StartAtUtc { get; init; }

    public DateTimeOffset EndAtUtc { get; init; }

    public string? TimeZoneId { get; init; }

    public bool AllDay { get; init; }

    public int LocationId { get; init; }

    public int CategoryId { get; init; }

    public int SubCategoryId { get; init; }

    public int Capacity { get; init; }

    public IReadOnlyCollection<ShiftEntryLinkRequest> ShiftEntryLinks { get; init; } = [];
}
