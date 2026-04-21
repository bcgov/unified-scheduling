namespace Unified.Stats.Models;

public sealed record StatSignoffResponse
{
    public int Id { get; init; }
    public Guid UserId { get; init; }
    public int LocationId { get; init; }
    public int Month { get; init; }
    public int Year { get; init; }
    public DateTimeOffset SignoffDate { get; init; }
    public DateTimeOffset CreatedOn { get; init; }
}
