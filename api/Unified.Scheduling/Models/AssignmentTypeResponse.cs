namespace Unified.Scheduling.Models;

public sealed record AssignmentTypeResponse
{
    public int Id { get; init; }

    public required string Code { get; init; }

    public required string Description { get; init; }

    public DateTimeOffset EffectiveDate { get; init; }

    public DateTimeOffset? ExpiryDate { get; init; }
}
