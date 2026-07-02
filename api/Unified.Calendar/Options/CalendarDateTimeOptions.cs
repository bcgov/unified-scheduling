using System.ComponentModel.DataAnnotations;

namespace Unified.Calendar.Options;

public sealed class CalendarDateTimeOptions
{
    public const string SectionName = "CalendarDateTime";

    [Required]
    public string DefaultTimeZoneId { get; set; } = string.Empty;
}
