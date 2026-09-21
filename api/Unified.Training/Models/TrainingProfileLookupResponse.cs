namespace Unified.Training.Models;

public sealed record TrainingProfileLookupResponse
{
    public required int Id { get; init; }

    public required string Code { get; init; }

    public required DateTimeOffset CreatedOn { get; init; }

    public DateTimeOffset? UpdatedOn { get; init; }
}
