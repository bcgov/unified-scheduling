namespace Unified.Scheduling.Models;

public sealed record AssignmentTypeRequest
{
    public int LocationId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public DateTimeOffset EffectiveDate { get; init; }

    public DateTimeOffset? ExpiryDate { get; init; }
}
