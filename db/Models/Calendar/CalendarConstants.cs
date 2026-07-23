namespace Unified.Db.Models.Calendar;

public static class CalendarConstants
{
    public const string SourceModule = "calendar";
}

public static class CalendarEventTypeCodes
{
    public const string General = "general";
    public const string Holiday = "holiday";
    public const string Deadline = "deadline";
    public const string AwayLocation = "awayLocation";
}

public static class CalendarEventStatusTypeCodes
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Cancelled = "cancelled";
}
