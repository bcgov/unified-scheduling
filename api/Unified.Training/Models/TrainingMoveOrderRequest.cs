namespace Unified.Training.Models;

public sealed record TrainingMoveOrderRequest
{
    public required int NewOrder { get; init; }
}
