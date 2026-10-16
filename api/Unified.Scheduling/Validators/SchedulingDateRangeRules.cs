namespace Unified.Scheduling.Validators;

internal static class SchedulingDateRangeRules
{
    public static readonly DateOnly MinimumDate = new(1900, 1, 1);
    public const int MaxRangeLengthDays = 366;

    public static bool IsSupportedRange(DateOnly startDate, DateOnly endDate) =>
        endDate.DayNumber - startDate.DayNumber + 1 <= MaxRangeLengthDays;
}
