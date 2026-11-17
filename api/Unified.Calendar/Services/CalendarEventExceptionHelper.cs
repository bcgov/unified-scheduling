using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public static class CalendarEventExceptionHelper
{
    public static void UpdateExceptionFlag(Event eventEntity)
    {
        SetReason(
            eventEntity,
            CalendarEventExceptionReasonCode.DateTimeChanged,
            eventEntity.EventSeriesId is not null
                && (
                    eventEntity.StartAtUtc != eventEntity.SeriesStartAtUtc
                    || eventEntity.EndAtUtc != eventEntity.SeriesEndAtUtc
                )
        );
    }

    public static void SetReason(Event eventEntity, CalendarEventExceptionReasonCode reason, bool present)
    {
        var code = reason.ToString();
        eventEntity.ExceptionReasonCodes = present
            ? eventEntity.ExceptionReasonCodes.Append(code).ToArray()
            : eventEntity.ExceptionReasonCodes.Where(existing => existing != code).ToArray();
    }

    public static CalendarEventExceptionReasonCode[] GetReasonCodes(Event eventEntity, bool loanOutEnabled = true) =>
        eventEntity
            .ExceptionReasonCodes.Where(code =>
                loanOutEnabled || code != nameof(CalendarEventExceptionReasonCode.IsAway)
            )
            .Select(Enum.Parse<CalendarEventExceptionReasonCode>)
            .ToArray();
}
