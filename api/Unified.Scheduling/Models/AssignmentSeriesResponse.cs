namespace Unified.Scheduling.Models;

public sealed record AssignmentSeriesResponse
{
    public int Id { get; init; }

    public int EventSeriesId { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? Notes { get; init; }

    public string? Color { get; init; }

    public string? RecurrenceRule { get; init; }

    public string? TimeZoneId { get; init; }

    public DateTimeOffset? StartAtUtc { get; init; }

    public DateTimeOffset? EndAtUtc { get; init; }

    public bool AllDay { get; init; }

    public string? EventTypeCode { get; init; }

    public string? StatusTypeCode { get; init; }

    public DateTimeOffset? CancelledAt { get; init; }

    public Guid? CancelledByUserId { get; init; }

    public string? CancellationReason { get; init; }

    public int? LocationId { get; init; }

    public int AssignmentCategoryTypeId { get; init; }

    public string? AssignmentCategoryTypeCode { get; init; }

    public int AssignmentSubCategoryTypeId { get; init; }

    public string? AssignmentSubCategoryTypeCode { get; init; }

    public int AssignmentTypeId { get; init; }

    public string? AssignmentTypeCode { get; init; }

    public int Capacity { get; init; }

    public IReadOnlyCollection<int> EventIds { get; init; } = [];

    public IReadOnlyCollection<int> AssignmentEntryIds { get; init; } = [];

    public IReadOnlyCollection<AssignmentEntryResponse> Entries { get; init; } = [];
}
