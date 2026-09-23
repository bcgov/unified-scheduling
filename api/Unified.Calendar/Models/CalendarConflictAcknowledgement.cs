namespace Unified.Calendar.Models;

public sealed record CalendarConflictAcknowledgement
{
    public required int FirstEventId { get; init; }

    public required int SecondEventId { get; init; }

    public required Guid ResourceId { get; init; }

    public required string Note { get; init; }
}
