namespace Unified.Calendar.Services;

public interface IRecurrenceRuleValidator
{
    RecurrenceValidationResult Validate(
        string recurrenceRule,
        DateTimeOffset seriesStartAtUtc,
        DateTimeOffset? seriesEndAtUtc,
        string? timeZoneId,
        RecurrenceValidationOptions options
    );
}
