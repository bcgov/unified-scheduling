using Unified.Calendar.Services;

namespace Unified.Scheduling.Services;

public sealed record AssignmentSeriesMaterializationContext : IEventSeriesMaterializationContext
{
    public int AssignmentSeriesId { get; init; }

    public int AssignmentDefinitionId { get; init; }

    public int Capacity { get; init; }
}
