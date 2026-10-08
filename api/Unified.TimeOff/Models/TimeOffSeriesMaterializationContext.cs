using Unified.Db.Models.TimeOff;

namespace Unified.TimeOff.Models;

public sealed record TimeOffSeriesMaterializationContext
{
    public required TimeOffSeries TimeOffSeries { get; init; }

    public required IReadOnlyCollection<Guid> UserIds { get; init; }

    public IReadOnlyCollection<TimeOffEntry> ExistingEntries { get; init; } = [];
}
