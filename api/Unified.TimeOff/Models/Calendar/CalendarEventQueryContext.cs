namespace Unified.TimeOff.Services;

public sealed record CalendarEventQueryContext
{
    public required DateTimeOffset StartAtUtc { get; init; }

    public required DateTimeOffset EndAtUtc { get; init; }

    public int? LocationId { get; init; }

    public IReadOnlyCollection<Guid> UserIds { get; init; } = [];
}
