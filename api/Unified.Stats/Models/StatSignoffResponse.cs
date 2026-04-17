namespace Unified.Stats.Models;

public sealed record StatSignoffResponse(
    int Id,
    Guid UserId,
    int LocationId,
    int Month,
    int Year,
    DateTimeOffset SignoffDate,
    DateTimeOffset CreatedOn
);
