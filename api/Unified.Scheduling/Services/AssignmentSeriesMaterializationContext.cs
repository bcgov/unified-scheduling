using Unified.Db.Models.Scheduling;

namespace Unified.Scheduling.Services;

public sealed record AssignmentSeriesMaterializationContext
{
    public required AssignmentSeries AssignmentSeries { get; init; }

    public IReadOnlyCollection<AssignmentEntry> ExistingEntries { get; init; } = [];
}
