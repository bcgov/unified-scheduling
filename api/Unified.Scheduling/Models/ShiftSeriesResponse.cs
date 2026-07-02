namespace Unified.Scheduling.Models;

public sealed record ShiftSeriesResponse
{
    public int Id { get; init; }

    public int EventSeriesId { get; init; }

    public IReadOnlyCollection<Guid> UserIds { get; init; } = [];

    public IReadOnlyCollection<int> EventIds { get; init; } = [];

    public IReadOnlyCollection<int> ShiftEntryIds { get; init; } = [];
}
