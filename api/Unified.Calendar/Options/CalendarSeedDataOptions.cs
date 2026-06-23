using System.ComponentModel.DataAnnotations;

namespace Unified.Calendar.Options;

public sealed class CalendarSeedDataOptions
{
    public const string SectionName = "CalendarSeedData";

    [Required]
    public string HolidaysFilePath { get; set; } = string.Empty;
}
