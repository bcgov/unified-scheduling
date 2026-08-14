namespace Unified.Calendar.Services;

public sealed record RecurrenceValidationOptions
{
    public required TimeSpan MaximumDuration { get; init; }

    public required int MaximumOccurrences { get; init; }

    public bool RequireBoundedRule { get; init; } = true;
}
