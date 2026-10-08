namespace Unified.TimeOff.Models;

public sealed record LeaveTypeRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsPaid { get; init; }
    public DateTimeOffset? EffectiveDateUtc { get; init; }
    public DateTimeOffset? ExpiryDateUtc { get; init; }
}
