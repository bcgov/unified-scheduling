namespace Unified.Training.Models;

public sealed record UserTrainingResponse
{
    public required int Id { get; init; }

    public required Guid UserId { get; init; }

    public required int TrainingId { get; init; }

    public required string TrainingCode { get; init; }

    public required string TrainingCategoryName { get; init; }

    public required DateTimeOffset AwardedOn { get; init; }

    public DateTimeOffset? ExpiryDate { get; init; }

    public required string NoticeState { get; init; }

    public string? Notes { get; init; }

    public DateTimeOffset CreatedOn { get; init; }

    public DateTimeOffset? UpdatedOn { get; init; }
}
