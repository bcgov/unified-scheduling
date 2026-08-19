namespace Unified.Calendar.Holidays;

public sealed class BcStatutoryHolidayCalculator : IStatutoryHolidayCalculator
{
    public IReadOnlyList<StatutoryHoliday> Calculate(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("End date must be on or after start date.", nameof(endDate));
        }

        var holidays = new List<StatutoryHoliday>();

        for (var year = startDate.Year; year <= endDate.Year; year++)
        {
            holidays.AddRange(
                CalculateHolidaysForYear(year).Where(holiday => holiday.Date >= startDate && holiday.Date <= endDate)
            );
        }

        return holidays;
    }

    private static IReadOnlyList<StatutoryHoliday> CalculateHolidaysForYear(int year)
    {
        var easterSunday = CalculateGregorianEasterSunday(year);

        var holidays = new List<StatutoryHoliday>
        {
            CreateHoliday(StatutoryHolidayType.NewYearsDay, "New Years Day", new DateOnly(year, 1, 1)),
            CreateHoliday(
                StatutoryHolidayType.FamilyDay,
                "Family Day",
                GetNthWeekdayOfMonth(year, 2, DayOfWeek.Monday, 3)
            ),
            CreateHoliday(StatutoryHolidayType.GoodFriday, "Good Friday", easterSunday.AddDays(-2)),
            CreateHoliday(StatutoryHolidayType.EasterMonday, "Easter Monday", easterSunday.AddDays(1)),
            CreateHoliday(StatutoryHolidayType.VictoriaDay, "Victoria Day", GetVictoriaDay(year)),
            CreateHoliday(StatutoryHolidayType.CanadaDay, "Canada Day", new DateOnly(year, 7, 1)),
            CreateHoliday(StatutoryHolidayType.BcDay, "BC Day", GetNthWeekdayOfMonth(year, 8, DayOfWeek.Monday, 1)),
            CreateHoliday(
                StatutoryHolidayType.LabourDay,
                "Labour Day",
                GetNthWeekdayOfMonth(year, 9, DayOfWeek.Monday, 1)
            ),
        };

        if (year >= 2023)
        {
            holidays.Add(
                CreateHoliday(
                    StatutoryHolidayType.TruthAndReconciliation,
                    "National Day for Truth and Reconciliation",
                    new DateOnly(year, 9, 30)
                )
            );
        }

        holidays.AddRange([
            CreateHoliday(
                StatutoryHolidayType.Thanksgiving,
                "Thanksgiving",
                GetNthWeekdayOfMonth(year, 10, DayOfWeek.Monday, 2)
            ),
            CreateHoliday(StatutoryHolidayType.RemembranceDay, "Remembrance Day", new DateOnly(year, 11, 11)),
            CreateHoliday(StatutoryHolidayType.Christmas, "Christmas", new DateOnly(year, 12, 25)),
            CreateHoliday(StatutoryHolidayType.BoxingDay, "Boxing Day", new DateOnly(year, 12, 26)),
        ]);

        return holidays;
    }

    private static StatutoryHoliday CreateHoliday(StatutoryHolidayType type, string name, DateOnly nominalDate)
    {
        var observedDate =
            type == StatutoryHolidayType.BoxingDay
                ? ApplyBoxingDayObservation(nominalDate)
                : ApplyWeekendObservation(nominalDate);

        return new(type, name, observedDate);
    }

    private static DateOnly GetNthWeekdayOfMonth(int year, int month, DayOfWeek dayOfWeek, int occurrence)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(occurrence);

        var date = new DateOnly(year, month, 1);
        var daysUntilWeekday = ((int)dayOfWeek - (int)date.DayOfWeek + 7) % 7;
        var result = date.AddDays(daysUntilWeekday + ((occurrence - 1) * 7));

        if (result.Month != month)
        {
            throw new InvalidOperationException(
                $"The {occurrence} occurrence of {dayOfWeek} does not exist in {year}-{month:00}."
            );
        }

        return result;
    }

    private static DateOnly GetVictoriaDay(int year)
    {
        var date = new DateOnly(year, 5, 24);

        while (date.DayOfWeek != DayOfWeek.Monday)
        {
            date = date.AddDays(-1);
        }

        return date;
    }

    private static DateOnly ApplyBoxingDayObservation(DateOnly boxingDay)
    {
        var speciallyObservedDate = boxingDay.DayOfWeek switch
        {
            DayOfWeek.Sunday => boxingDay.AddDays(2),
            DayOfWeek.Monday => boxingDay.AddDays(1),
            _ => boxingDay,
        };

        return ApplyWeekendObservation(speciallyObservedDate);
    }

    private static DateOnly ApplyWeekendObservation(DateOnly date) =>
        date.DayOfWeek switch
        {
            DayOfWeek.Saturday => date.AddDays(2),
            DayOfWeek.Sunday => date.AddDays(1),
            _ => date,
        };

    private static DateOnly CalculateGregorianEasterSunday(int year)
    {
        var goldenNumber = NonNegativeModulo(year, 19) + 1;
        var century = (year / 100) + 1;
        var solarCorrection = ((3 * century) / 4) - 12;
        var moonCorrection = ((8 * century + 5) / 25) - 5;
        var dominicalNumber = ((5 * year) / 4) - solarCorrection - 10;
        var epact = NonNegativeModulo(11 * goldenNumber + 20 + moonCorrection - solarCorrection, 30);

        if (epact == 24 || (epact == 25 && goldenNumber > 11))
        {
            epact++;
        }

        var paschalFullMoon = 44 - epact;

        if (paschalFullMoon < 21)
        {
            paschalFullMoon += 30;
        }

        var easterDay = (paschalFullMoon + 7) - NonNegativeModulo(dominicalNumber + paschalFullMoon, 7);

        return easterDay > 31 ? new DateOnly(year, 4, easterDay - 31) : new DateOnly(year, 3, easterDay);
    }

    private static int NonNegativeModulo(int value, int divisor) => ((value % divisor) + divisor) % divisor;
}
