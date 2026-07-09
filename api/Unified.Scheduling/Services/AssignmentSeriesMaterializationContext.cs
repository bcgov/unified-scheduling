using Unified.Calendar.Services;

namespace Unified.Scheduling.Services;

public sealed record AssignmentSeriesMaterializationContext : IEventSeriesMaterializationContext
{
    public int AssignmentSeriesId { get; init; }

    public int AssignmentCategoryTypeId { get; init; }

    public int AssignmentSubCategoryTypeId { get; init; }

    public int AssignmentTypeId { get; init; }

    public int Capacity { get; init; }
}
