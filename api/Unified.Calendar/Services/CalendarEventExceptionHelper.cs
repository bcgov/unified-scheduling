using Unified.Db.Models.Calendar;

namespace Unified.Calendar.Services;

public static class CalendarEventExceptionHelper
{
    public static void UpdateExceptionFlag(Event eventEntity)
    {
        if (eventEntity.EventSeriesId is null)
        {
            eventEntity.IsException = false;
            return;
        }

        eventEntity.IsException =
            eventEntity.StartAtUtc != eventEntity.SeriesStartAtUtc
            || eventEntity.EndAtUtc != eventEntity.SeriesEndAtUtc;
    }
}
