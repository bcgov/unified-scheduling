namespace Unified.Calendar.Models;

public sealed record CalendarConflictAcknowledgement
{
    public required string FirstSourceModule { get; init; }

    public required string FirstEventId { get; init; }

    public required string SecondSourceModule { get; init; }

    public required string SecondEventId { get; init; }

    public required Guid ResourceId { get; init; }

    public required string Note { get; init; }
}
