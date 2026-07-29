using Unified.Calendar.Services;
using Unified.Db.Models.Scheduling;

namespace Unified.Scheduling.Services;

public sealed record ShiftSeriesMaterializationContext
{
    public required ShiftSeries ShiftSeries { get; init; }

    public required IReadOnlyCollection<Guid> UserIds { get; init; }

    public IReadOnlyCollection<ShiftEntry> ExistingEntries { get; init; } = [];
}
