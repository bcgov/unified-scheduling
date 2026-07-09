namespace Unified.Scheduling.Models;

public sealed record AssignmentEntryResponse
{
    public int Id { get; init; }

    public int? AssignmentSeriesId { get; init; }

    public int EventId { get; init; }

    public int AssignmentCategoryTypeId { get; init; }

    public string? AssignmentCategoryTypeCode { get; init; }

    public int AssignmentSubCategoryTypeId { get; init; }

    public string? AssignmentSubCategoryTypeCode { get; init; }

    public int AssignmentTypeId { get; init; }

    public string? AssignmentTypeCode { get; init; }

    public int Capacity { get; init; }

    public int AssignedUserCount { get; init; }

    public IReadOnlyCollection<int> LinkedShiftEntryIds { get; init; } = [];

    public IReadOnlyCollection<Guid> AssignedUserIds { get; init; } = [];
}
