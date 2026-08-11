namespace Unified.Calendar.Models;

public sealed record CalendarConflictOverrideRequest
{
    public int FirstEventId { get; init; }

    public int SecondEventId { get; init; }

    public string Note { get; init; } = string.Empty;
}

public sealed record CalendarConflictOverrideResponse(
    int Id,
    int FirstEventId,
    int SecondEventId,
    string Note,
    Guid? CreatedById,
    DateTimeOffset CreatedOn,
    Guid? UpdatedById,
    DateTimeOffset? UpdatedOn
);
