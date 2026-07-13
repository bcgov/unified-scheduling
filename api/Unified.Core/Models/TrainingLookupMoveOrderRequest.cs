namespace Unified.Core.Models;

public sealed record TrainingLookupMoveOrderRequest
{
    public required int NewOrder { get; init; }
}