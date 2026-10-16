namespace Unified.TimeOff.Models;

public sealed record LeaveTypeResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsPaid { get; init; }
    public DateTimeOffset EffectiveDateUtc { get; init; }
    public DateTimeOffset? ExpiryDateUtc { get; init; }
}
