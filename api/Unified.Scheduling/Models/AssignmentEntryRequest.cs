namespace Unified.Scheduling.Models;

public sealed record AssignmentEntryRequest : IAssignmentRequestFields
{
    public int? AssignmentSeriesId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? Notes { get; init; }

    public string? Color { get; init; }

    public DateTimeOffset StartAtUtc { get; init; }

    public DateTimeOffset? EndAtUtc { get; init; }

    public DateTimeOffset? SeriesStartAtUtc { get; init; }

    public DateTimeOffset? SeriesEndAtUtc { get; init; }

    public string? TimeZoneId { get; init; }

    public bool AllDay { get; init; }

    public bool IsException { get; init; }

    public int? LocationId { get; init; }

    public int AssignmentCategoryTypeId { get; init; }

    public int AssignmentSubCategoryTypeId { get; init; }

    public int AssignmentTypeId { get; init; }

    public int Capacity { get; init; }
}
