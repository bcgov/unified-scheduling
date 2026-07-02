using Unified.Calendar.Services;

namespace Unified.Scheduling.Services;

public sealed record ShiftSeriesMaterializationContext : IEventSeriesMaterializationContext
{
    public required int ShiftSeriesId { get; init; }

    public required IReadOnlyCollection<Guid> UserIds { get; init; }
}
