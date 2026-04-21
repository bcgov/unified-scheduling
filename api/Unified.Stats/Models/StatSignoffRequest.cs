namespace Unified.Stats.Models;

public sealed record StatSignoffRequest
{
    public required Guid UserId { get; init; }
    public required int LocationId { get; init; }
    public required int Month { get; init; }
    public required int Year { get; init; }
    public required DateTimeOffset SignoffDate { get; init; }
}
