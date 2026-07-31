namespace Unified.Calendar.Holidays;

public interface IStatutoryHolidayCalculator
{
    IReadOnlyList<StatutoryHoliday> Calculate(DateOnly startDate, DateOnly endDate);
}
