namespace Unified.Calendar;

public static class CalendarConstants
{
    public const string SourceModule = Unified.Db.Models.Calendar.CalendarConstants.SourceModule;
}

public static class CalendarEventTypeCodes
{
    public const string General = Unified.Db.Models.Calendar.CalendarEventTypeCodes.General;
    public const string Holiday = Unified.Db.Models.Calendar.CalendarEventTypeCodes.Holiday;
    public const string Deadline = Unified.Db.Models.Calendar.CalendarEventTypeCodes.Deadline;
}

public static class CalendarEventStatusTypeCodes
{
    public const string Draft = Unified.Db.Models.Calendar.CalendarEventStatusTypeCodes.Draft;
    public const string Active = Unified.Db.Models.Calendar.CalendarEventStatusTypeCodes.Active;
    public const string Cancelled = Unified.Db.Models.Calendar.CalendarEventStatusTypeCodes.Cancelled;
}