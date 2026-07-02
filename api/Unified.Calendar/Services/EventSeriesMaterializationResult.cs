namespace Unified.Calendar.Services;

public sealed record EventSeriesMaterializationResult
{
    public int CreatedCount { get; init; }

    public int UpdatedCount { get; init; }

    public int RemovedCount { get; init; }

    public IReadOnlyCollection<int> CreatedEventIds { get; init; } = [];

    public IReadOnlyCollection<int> UpdatedEventIds { get; init; } = [];

    public IReadOnlyCollection<int> RemovedEventIds { get; init; } = [];

    public IReadOnlyCollection<SkippedMaterializedEvent> SkippedEvents { get; init; } = [];
}
