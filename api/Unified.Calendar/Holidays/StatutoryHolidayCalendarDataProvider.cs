using System.Globalization;
using Unified.Calendar.Models;
using Unified.Common.Time;

namespace Unified.Calendar.Holidays;

public sealed class StatutoryHolidayCalendarDataProvider(
    IStatutoryHolidayCalculator holidayCalculator,
    ITimeZoneService timeZoneService
)
{
    public IReadOnlyCollection<CalendarEventResponse> GetEvents(
        DateOnly startDate,
        DateOnly endDate,
        TimeZoneInfo timeZone
    ) => holidayCalculator.Calculate(startDate, endDate).Select(holiday => MapToResponse(holiday, timeZone)).ToList();

    private CalendarEventResponse MapToResponse(StatutoryHoliday holiday, TimeZoneInfo timeZone)
    {
        var startAtUtc = timeZoneService.ToUtcInstant(holiday.Date.ToDateTime(TimeOnly.MinValue), timeZone);
        var endAtUtc = timeZoneService.ToUtcInstant(holiday.Date.AddDays(1).ToDateTime(TimeOnly.MinValue), timeZone);

        return new CalendarEventResponse
        {
            Id = $"stat-holiday:{holiday.Type}:{holiday.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
            Title = holiday.Name,
            StartAtUtc = startAtUtc,
            EndAtUtc = endAtUtc,
            TimeZoneId = timeZone.Id,
            AllDay = true,
            IsReadOnly = true,
            IsException = false,
            HolidayType = holiday.Type,
            EventTypeCode = CalendarEventTypeCode.Holiday,
            StatusTypeCode = CalendarEventStatusTypeCode.Active,
            SourceModule = CalendarConstants.SourceModule,
        };
    }
}
