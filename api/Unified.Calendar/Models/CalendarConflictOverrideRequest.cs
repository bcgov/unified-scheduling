using System.ComponentModel.DataAnnotations;

namespace Unified.Calendar.Models;

public sealed record CalendarConflictOverrideRequest
{
    [Required]
    public required int FirstEventId { get; init; }

    [Required]
    public required int SecondEventId { get; init; }

    [Required]
    public required Guid ResourceId { get; init; }

    [Required]
    public required string Note { get; init; }
}

public sealed record CalendarConflictOverrideResponse(
    int Id,
    int FirstEventId,
    int SecondEventId,
    Guid ResourceId,
    string Note,
    Guid? CreatedById,
    DateTimeOffset CreatedOn,
    Guid? UpdatedById,
    DateTimeOffset? UpdatedOn
);
