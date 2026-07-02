namespace Unified.Calendar.Services;

public sealed record SeriesEntry
{
    public required DateTimeOffset StartAtUtc { get; init; }

    public DateTimeOffset? EndAtUtc { get; init; }
}
