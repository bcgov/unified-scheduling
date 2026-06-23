using System.Text.Json.Serialization;

namespace Unified.Calendar;

public static class CalendarConstants
{
    public const string SourceModule = Unified.Db.Models.Calendar.CalendarConstants.SourceModule;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CalendarEventTypeCode
{
    General,
    Holiday,
    Deadline,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CalendarEventStatusTypeCode
{
    Draft,
    Active,
    Cancelled,
}

public static class CalendarCodeMappings
{
    public static string ToDbCode(CalendarEventTypeCode code) =>
        code switch
        {
            CalendarEventTypeCode.General => Unified.Db.Models.Calendar.CalendarEventTypeCodes.General,
            CalendarEventTypeCode.Holiday => Unified.Db.Models.Calendar.CalendarEventTypeCodes.Holiday,
            CalendarEventTypeCode.Deadline => Unified.Db.Models.Calendar.CalendarEventTypeCodes.Deadline,
            _ => throw new InvalidOperationException($"Unknown calendar event type enum '{code}'."),
        };

    public static string ToDbCode(CalendarEventStatusTypeCode code) =>
        code switch
        {
            CalendarEventStatusTypeCode.Draft => Unified.Db.Models.Calendar.CalendarEventStatusTypeCodes.Draft,
            CalendarEventStatusTypeCode.Active => Unified.Db.Models.Calendar.CalendarEventStatusTypeCodes.Active,
            CalendarEventStatusTypeCode.Cancelled => Unified.Db.Models.Calendar.CalendarEventStatusTypeCodes.Cancelled,
            _ => throw new InvalidOperationException($"Unknown calendar event status enum '{code}'."),
        };

    public static CalendarEventTypeCode ToEventTypeCode(string dbCode) =>
        dbCode switch
        {
            Unified.Db.Models.Calendar.CalendarEventTypeCodes.General => CalendarEventTypeCode.General,
            Unified.Db.Models.Calendar.CalendarEventTypeCodes.Holiday => CalendarEventTypeCode.Holiday,
            Unified.Db.Models.Calendar.CalendarEventTypeCodes.Deadline => CalendarEventTypeCode.Deadline,
            _ => throw new InvalidOperationException($"Unknown calendar event type code '{dbCode}'."),
        };

    public static CalendarEventStatusTypeCode ToStatusTypeCode(string dbCode) =>
        dbCode switch
        {
            Unified.Db.Models.Calendar.CalendarEventStatusTypeCodes.Draft => CalendarEventStatusTypeCode.Draft,
            Unified.Db.Models.Calendar.CalendarEventStatusTypeCodes.Active => CalendarEventStatusTypeCode.Active,
            Unified.Db.Models.Calendar.CalendarEventStatusTypeCodes.Cancelled => CalendarEventStatusTypeCode.Cancelled,
            _ => throw new InvalidOperationException($"Unknown calendar event status code '{dbCode}'."),
        };
}
