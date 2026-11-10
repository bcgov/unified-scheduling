namespace Unified.Training.Models;

public sealed record UserTrainingExpiryDateRequest
{
    public required int TrainingId { get; init; }

    public required DateTimeOffset AwardedOn { get; init; }

    public Guid? UserId { get; init; }

    public bool? AllDay { get; init; }
}
