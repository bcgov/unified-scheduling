namespace Unified.Calendar.Services;

public sealed record RecurrenceValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public IReadOnlyCollection<string> Errors { get; init; } = [];

    public static RecurrenceValidationResult Success { get; } = new();

    public static RecurrenceValidationResult Failure(params string[] errors) => new() { Errors = errors };
}
