namespace Unified.Training.Models;

public sealed record UserTrainingExpiryDateResponse
{
    public DateTimeOffset? ExpiryDate { get; init; }
}
