using System.Text.Json.Serialization;

namespace Unified.Calendar;

public static class CalendarConstants
{
    public const string SourceModule = Unified.Db.Models.Calendar.CalendarConstants.SourceModule;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CalendarEventType
{
    [JsonStringEnumMemberName("calendar.event")]
    CalendarEvent,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CalendarEventStatus
{
    [JsonStringEnumMemberName("Active")]
    Active,

    [JsonStringEnumMemberName("Draft")]
    Draft,

    [JsonStringEnumMemberName("Draft Item")]
    DraftItem,

    [JsonStringEnumMemberName("Cancelled")]
    Cancelled,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CalendarEventTypeCode
{
    General,
    Holiday,
    Deadline,
    AwayLocation,
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
            CalendarEventTypeCode.AwayLocation => Unified.Db.Models.Calendar.CalendarEventTypeCodes.AwayLocation,
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

    public static CalendarEventStatus ToEventStatus(string dbCode) =>
        dbCode switch
        {
            Unified.Db.Models.Calendar.CalendarEventStatusTypeCodes.Draft => CalendarEventStatus.Draft,
            Unified.Db.Models.Calendar.CalendarEventStatusTypeCodes.Active => CalendarEventStatus.Active,
            Unified.Db.Models.Calendar.CalendarEventStatusTypeCodes.Cancelled => CalendarEventStatus.Cancelled,
            _ => throw new InvalidOperationException($"Unknown calendar event status code '{dbCode}'."),
        };

    public static CalendarEventTypeCode ToEventTypeCode(string dbCode) =>
        dbCode switch
        {
            Unified.Db.Models.Calendar.CalendarEventTypeCodes.General => CalendarEventTypeCode.General,
            Unified.Db.Models.Calendar.CalendarEventTypeCodes.Holiday => CalendarEventTypeCode.Holiday,
            Unified.Db.Models.Calendar.CalendarEventTypeCodes.Deadline => CalendarEventTypeCode.Deadline,
            Unified.Db.Models.Calendar.CalendarEventTypeCodes.AwayLocation => CalendarEventTypeCode.AwayLocation,
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
