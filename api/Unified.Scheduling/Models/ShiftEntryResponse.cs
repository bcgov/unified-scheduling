namespace Unified.Scheduling.Models;

public sealed record ShiftEntryResponse
{
    public int Id { get; init; }

    public int? ShiftSeriesId { get; init; }

    public int EventId { get; init; }

    public IReadOnlyCollection<Guid> UserIds { get; init; } = [];

    public IReadOnlyCollection<ShiftAssignmentEntryResponse> AssignmentLinks { get; init; } = [];
}
