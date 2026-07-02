namespace Unified.Calendar.Services;

public sealed record RecurrenceCountResult
{
    public required int Count { get; init; }

    public required bool StoppedEarly { get; init; }
}
