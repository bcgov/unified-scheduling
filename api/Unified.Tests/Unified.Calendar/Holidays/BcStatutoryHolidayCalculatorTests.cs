using Unified.Calendar.Holidays;

namespace Unified.Tests.Calendar.Holidays;

public sealed class BcStatutoryHolidayCalculatorTests
{
    private static readonly StatutoryHolidayType[] ExpectedHolidayOrder =
    [
        StatutoryHolidayType.NewYearsDay,
        StatutoryHolidayType.FamilyDay,
        StatutoryHolidayType.GoodFriday,
        StatutoryHolidayType.EasterMonday,
        StatutoryHolidayType.VictoriaDay,
        StatutoryHolidayType.CanadaDay,
        StatutoryHolidayType.BcDay,
        StatutoryHolidayType.LabourDay,
        StatutoryHolidayType.TruthAndReconciliation,
        StatutoryHolidayType.Thanksgiving,
        StatutoryHolidayType.RemembranceDay,
        StatutoryHolidayType.Christmas,
        StatutoryHolidayType.BoxingDay,
    ];

    private readonly BcStatutoryHolidayCalculator _calculator = new();

    [Fact]
    public void Calculate_WhenEndDatePrecedesStartDate_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _calculator.Calculate(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 1))
        );
    }

    [Fact]
    public void Calculate_ForSingleBoundaryDate_IncludesOnlyHolidayOnThatDate()
    {
        var holidays = _calculator.Calculate(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1));

        var holiday = Assert.Single(holidays);
        Assert.Equal(StatutoryHolidayType.CanadaDay, holiday.Type);
        Assert.Equal("Canada Day", holiday.Name);
        Assert.Equal(new DateOnly(2026, 7, 1), holiday.Date);
    }

    [Fact]
    public void Calculate_ForPartialRange_IncludesBothBoundariesAndExcludesOutsideDates()
    {
        var holidays = _calculator.Calculate(new DateOnly(2026, 2, 9), new DateOnly(2026, 4, 6));

        Assert.Equal(
            [
                (StatutoryHolidayType.FamilyDay, new DateOnly(2026, 2, 16)),
                (StatutoryHolidayType.GoodFriday, new DateOnly(2026, 4, 3)),
                (StatutoryHolidayType.EasterMonday, new DateOnly(2026, 4, 6)),
            ],
            holidays.Select(holiday => (holiday.Type, holiday.Date))
        );
    }

    [Fact]
    public void Calculate_ForMultipleYears_ReturnsEachYearInFixedTypeOrder()
    {
        var holidays = _calculator.Calculate(new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31));

        Assert.Equal(ExpectedHolidayOrder.Length * 2, holidays.Count);
        Assert.Equal(ExpectedHolidayOrder, holidays.Take(ExpectedHolidayOrder.Length).Select(holiday => holiday.Type));
        Assert.Equal(ExpectedHolidayOrder, holidays.Skip(ExpectedHolidayOrder.Length).Select(holiday => holiday.Type));
    }

    [Fact]
    public void Calculate_Before2023_PreservesOrderWithoutTruthAndReconciliationDay()
    {
        var holidays = _calculator.Calculate(new DateOnly(2022, 1, 1), new DateOnly(2022, 12, 31));

        Assert.Equal(
            ExpectedHolidayOrder.Where(type => type != StatutoryHolidayType.TruthAndReconciliation),
            holidays.Select(holiday => holiday.Type)
        );
    }

    [Fact]
    public void Calculate_PreservesExactLegacyNamesAndHolidaySet()
    {
        var holidays = _calculator.Calculate(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        Assert.Equal(
            [
                "New Years Day",
                "Family Day",
                "Good Friday",
                "Easter Monday",
                "Victoria Day",
                "Canada Day",
                "BC Day",
                "Labour Day",
                "National Day for Truth and Reconciliation",
                "Thanksgiving",
                "Remembrance Day",
                "Christmas",
                "Boxing Day",
            ],
            holidays.Select(holiday => holiday.Name)
        );
    }

    [Fact]
    public void Calculate_For2022_DoesNotIncludeTruthAndReconciliationDay()
    {
        var holidays = _calculator.Calculate(new DateOnly(2022, 1, 1), new DateOnly(2022, 12, 31));

        Assert.DoesNotContain(holidays, holiday => holiday.Type == StatutoryHolidayType.TruthAndReconciliation);
    }

    [Fact]
    public void Calculate_ForSeptember30In2022_DoesNotReturnTruthAndReconciliationDay()
    {
        var holidays = _calculator.Calculate(new DateOnly(2022, 9, 30), new DateOnly(2022, 9, 30));

        Assert.DoesNotContain(holidays, holiday => holiday.Type == StatutoryHolidayType.TruthAndReconciliation);
    }

    [Fact]
    public void Calculate_For2023_ObservesTruthAndReconciliationDayOnOctober2()
    {
        var holidays = _calculator.Calculate(new DateOnly(2023, 1, 1), new DateOnly(2023, 12, 31));

        var holiday = Find(holidays, StatutoryHolidayType.TruthAndReconciliation);
        Assert.Equal("National Day for Truth and Reconciliation", holiday.Name);
        Assert.Equal(new DateOnly(2023, 10, 2), holiday.Date);
    }

    [Fact]
    public void Calculate_WhenRangeEndsOnNominalTruthAndReconciliationDate_FiltersByObservedDate()
    {
        var holidays = _calculator.Calculate(new DateOnly(2023, 9, 1), new DateOnly(2023, 9, 30));

        Assert.DoesNotContain(holidays, holiday => holiday.Type == StatutoryHolidayType.TruthAndReconciliation);
    }

    [Fact]
    public void Calculate_WhenRangeContainsObservedTruthAndReconciliationDate_ReturnsHoliday()
    {
        var holidays = _calculator.Calculate(new DateOnly(2023, 10, 2), new DateOnly(2023, 10, 2));

        var holiday = Assert.Single(holidays);
        Assert.Equal(StatutoryHolidayType.TruthAndReconciliation, holiday.Type);
        Assert.Equal(new DateOnly(2023, 10, 2), holiday.Date);
    }

    [Fact]
    public void Calculate_For2024_ReturnsTruthAndReconciliationDayOnSeptember30()
    {
        var holidays = _calculator.Calculate(new DateOnly(2024, 9, 30), new DateOnly(2024, 9, 30));

        var holiday = Assert.Single(holidays);
        Assert.Equal(StatutoryHolidayType.TruthAndReconciliation, holiday.Type);
        Assert.Equal(new DateOnly(2024, 9, 30), holiday.Date);
    }

    [Fact]
    public void Calculate_For2029_ObservesTruthAndReconciliationDayOnOctober1()
    {
        var holidays = _calculator.Calculate(new DateOnly(2029, 9, 30), new DateOnly(2029, 10, 1));

        var holiday = Assert.Single(holidays);
        Assert.Equal(StatutoryHolidayType.TruthAndReconciliation, holiday.Type);
        Assert.Equal(new DateOnly(2029, 10, 1), holiday.Date);
    }

    [Fact]
    public void Calculate_ForMultiYearRange_IncludesTruthAndReconciliationDayOnlyFrom2023Onward()
    {
        var holidays = _calculator.Calculate(new DateOnly(2022, 9, 30), new DateOnly(2024, 9, 30));

        Assert.Equal(
            [new DateOnly(2023, 10, 2), new DateOnly(2024, 9, 30)],
            holidays
                .Where(holiday => holiday.Type == StatutoryHolidayType.TruthAndReconciliation)
                .Select(holiday => holiday.Date)
        );
    }

    [Fact]
    public void Calculate_FamilyDay_UsesThirdMondayInFebruary()
    {
        var holidays = _calculator.Calculate(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));

        var familyDay = Find(holidays, StatutoryHolidayType.FamilyDay);
        Assert.Equal(new DateOnly(2026, 2, 16), familyDay.Date);
        Assert.NotEqual(new DateOnly(2026, 2, 9), familyDay.Date);
    }

    [Theory]
    [InlineData(2022, 1, 3)]
    [InlineData(2023, 1, 2)]
    public void Calculate_ObservesWeekendNewYearsDay(int year, int expectedMonth, int expectedDay)
    {
        AssertHolidayDate(year, StatutoryHolidayType.NewYearsDay, expectedMonth, expectedDay);
    }

    [Theory]
    [InlineData(2023, 7, 3)]
    [InlineData(2018, 7, 2)]
    public void Calculate_ObservesWeekendCanadaDay(int year, int expectedMonth, int expectedDay)
    {
        AssertHolidayDate(year, StatutoryHolidayType.CanadaDay, expectedMonth, expectedDay);
    }

    [Theory]
    [InlineData(2023, 11, 13)]
    [InlineData(2018, 11, 12)]
    public void Calculate_ObservesWeekendRemembranceDay(int year, int expectedMonth, int expectedDay)
    {
        AssertHolidayDate(year, StatutoryHolidayType.RemembranceDay, expectedMonth, expectedDay);
    }

    [Theory]
    [InlineData(2021, 12, 27)]
    [InlineData(2022, 12, 26)]
    public void Calculate_ObservesWeekendChristmas(int year, int expectedMonth, int expectedDay)
    {
        AssertHolidayDate(year, StatutoryHolidayType.Christmas, expectedMonth, expectedDay);
    }

    [Theory]
    [InlineData(2021, 12, 28)]
    [InlineData(2022, 12, 27)]
    public void Calculate_AppliesBoxingDaySpecialHandlingBeforeWeekendObservation(
        int year,
        int expectedMonth,
        int expectedDay
    )
    {
        AssertHolidayDate(year, StatutoryHolidayType.BoxingDay, expectedMonth, expectedDay);
    }

    [Fact]
    public void Calculate_CalculatesRelativeLegacyHolidays()
    {
        var holidays = _calculator.Calculate(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        Assert.Equal(new DateOnly(2026, 2, 16), Find(holidays, StatutoryHolidayType.FamilyDay).Date);
        Assert.Equal(new DateOnly(2026, 5, 18), Find(holidays, StatutoryHolidayType.VictoriaDay).Date);
        Assert.Equal(new DateOnly(2026, 8, 3), Find(holidays, StatutoryHolidayType.BcDay).Date);
        Assert.Equal(new DateOnly(2026, 9, 7), Find(holidays, StatutoryHolidayType.LabourDay).Date);
        Assert.Equal(new DateOnly(2026, 10, 12), Find(holidays, StatutoryHolidayType.Thanksgiving).Date);
    }

    [Theory]
    [InlineData(2024, 3, 29, 4, 1)]
    [InlineData(2025, 4, 18, 4, 21)]
    [InlineData(2038, 4, 23, 4, 26)]
    public void Calculate_UsesGregorianEasterForGoodFridayAndEasterMonday(
        int year,
        int goodFridayMonth,
        int goodFridayDay,
        int easterMondayMonth,
        int easterMondayDay
    )
    {
        var holidays = _calculator.Calculate(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));

        Assert.Equal(
            new DateOnly(year, goodFridayMonth, goodFridayDay),
            Find(holidays, StatutoryHolidayType.GoodFriday).Date
        );
        Assert.Equal(
            new DateOnly(year, easterMondayMonth, easterMondayDay),
            Find(holidays, StatutoryHolidayType.EasterMonday).Date
        );
    }

    private void AssertHolidayDate(int year, StatutoryHolidayType type, int expectedMonth, int expectedDay)
    {
        var holidays = _calculator.Calculate(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
        Assert.Equal(new DateOnly(year, expectedMonth, expectedDay), Find(holidays, type).Date);
    }

    private static StatutoryHoliday Find(IEnumerable<StatutoryHoliday> holidays, StatutoryHolidayType type) =>
        Assert.Single(holidays, holiday => holiday.Type == type);
}
