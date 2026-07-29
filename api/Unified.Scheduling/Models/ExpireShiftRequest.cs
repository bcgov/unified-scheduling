namespace Unified.Scheduling.Models;

public sealed record ExpireShiftRequest
{
    public string? CancellationReason { get; init; }
}
