namespace Unified.Calendar.Services;

public sealed record EventSeriesMaterializationOptions
{
    public required RecurrenceValidationOptions ValidationOptions { get; init; }
}
