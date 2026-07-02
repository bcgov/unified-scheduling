namespace Unified.Calendar.Services;

public sealed record SkippedMaterializedEvent
{
    public required int EventId { get; init; }

    public required string Reason { get; init; }
}
