using System.ComponentModel.DataAnnotations;

namespace Unified.Scheduling.Options;

public sealed class WorkingHoursOptions
{
    public const string SectionName = "WorkingHours";

    [Range(1, int.MaxValue, ErrorMessage = "FullWorkingDayMinutes must be greater than or equal to 1.")]
    public int FullWorkingDayMinutes { get; init; } = 420;

    [Range(0, 60, ErrorMessage = "DefaultLunchMinutes must be greater than or equal to 0.")]
    public int DefaultLunchMinutes { get; init; } = 60;

    [Range(1, 100, ErrorMessage = "MaxQueryRangeDays must be greater than or equal to 1, and cannot exceed 100.")]
    public int MaxQueryRangeDays { get; init; } = 100;
}