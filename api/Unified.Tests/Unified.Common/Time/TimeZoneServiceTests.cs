using Unified.Common.Time;

namespace Unified.Tests.Common.Time;

public class TimeZoneServiceTests
{
    private readonly ITimeZoneService _service = new TimeZoneService();

    [Fact]
    public void ResolveOrUtc_WhenTimeZoneExists_ReturnsTimeZone()
    {
        var result = _service.ResolveOrUtc("America/Vancouver");

        Assert.Equal("America/Vancouver", result.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Not/A_TimeZone")]
    public void ResolveOrUtc_WhenTimeZoneIsMissingOrInvalid_ReturnsUtc(string? timeZoneId)
    {
        var result = _service.ResolveOrUtc(timeZoneId);

        Assert.Equal(TimeZoneInfo.Utc, result);
    }

    [Fact]
    public void ToTimeZone_WhenUtcInstantIsInSummer_ReturnsVancouverTime()
    {
        var utcInstant = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

        var result = _service.ToTimeZone(utcInstant, "America/Vancouver");

        Assert.Equal(new DateTimeOffset(2026, 7, 1, 5, 0, 0, TimeSpan.FromHours(-7)), result);
    }

    [Theory]
    [InlineData("2026-07-01", 2026, 7, 1, 7)]
    [InlineData("2026-01-01", 2026, 1, 1, 8)]
    public void FromDateStringToStartOfDayInTimeZone_ReturnsExpectedUtcInstant(
        string dateString,
        int year,
        int month,
        int day,
        int hour
    )
    {
        var result = _service.FromDateStringToStartOfDayInTimeZone(dateString, "America/Vancouver");

        Assert.Equal(new DateTimeOffset(year, month, day, hour, 0, 0, TimeSpan.Zero), result);
    }

    [Theory]
    [InlineData("2026-07-01", 2026, 7, 2, 6)]
    [InlineData("2026-01-01", 2026, 1, 2, 7)]
    public void FromDateStringToEndOfDayInTimeZone_PreservesInclusiveMillisecondBoundary(
        string dateString,
        int year,
        int month,
        int day,
        int hour
    )
    {
        var result = _service.FromDateStringToEndOfDayInTimeZone(dateString, "America/Vancouver");

        Assert.Equal(new DateTimeOffset(year, month, day, hour, 59, 59, 999, TimeSpan.Zero), result);
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("2026-02-30")]
    public void FromDateStringToStartOfDayInTimeZone_WhenDateIsInvalid_Throws(string dateString)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            _service.FromDateStringToStartOfDayInTimeZone(dateString, "America/Vancouver")
        );

        Assert.Equal($"Invalid date format. Expected yyyy-MM-dd, got {dateString}", exception.Message);
    }

    [Fact]
    public void ToLocalUnspecified_WhenInstantIsValid_ReturnsLocalUnspecifiedTime()
    {
        var timeZone = _service.ResolveRequired("America/Vancouver");

        var result = _service.ToLocalUnspecified(new DateTimeOffset(2026, 7, 1, 16, 30, 0, TimeSpan.Zero), timeZone);

        Assert.Equal(new DateTime(2026, 7, 1, 9, 30, 0), result);
        Assert.Equal(DateTimeKind.Unspecified, result.Kind);
    }

    [Fact]
    public void ToUtcInstant_WhenLocalTimeIsValid_ReturnsUtcInstant()
    {
        var timeZone = _service.ResolveRequired("America/Vancouver");

        var result = _service.ToUtcInstant(new DateTime(2026, 7, 1, 9, 30, 0), timeZone);

        Assert.Equal(new DateTimeOffset(2026, 7, 1, 16, 30, 0, TimeSpan.Zero), result);
    }

    [Theory]
    [InlineData(2025, 3, 9, 2, 30, "invalid")]
    [InlineData(2025, 11, 2, 1, 30, "ambiguous")]
    public void ToUtcInstant_WhenLocalTimeIsInvalidOrAmbiguous_Throws(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        string expectedMessage
    )
    {
        var timeZone = CreatePacificTimeZone();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _service.ToUtcInstant(new DateTime(year, month, day, hour, minute, 0), timeZone)
        );

        Assert.Contains(expectedMessage, exception.Message);
    }

    private static TimeZoneInfo CreatePacificTimeZone()
    {
        var daylightStart = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 2, 0, 0),
            month: 3,
            week: 2,
            dayOfWeek: DayOfWeek.Sunday
        );
        var daylightEnd = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 2, 0, 0),
            month: 11,
            week: 1,
            dayOfWeek: DayOfWeek.Sunday
        );
        var adjustmentRule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2025, 1, 1),
            new DateTime(2025, 12, 31),
            TimeSpan.FromHours(1),
            daylightStart,
            daylightEnd
        );

        return TimeZoneInfo.CreateCustomTimeZone(
            "Test Pacific",
            TimeSpan.FromHours(-8),
            "Test Pacific",
            "Pacific Standard Time",
            "Pacific Daylight Time",
            [adjustmentRule]
        );
    }

    [Fact]
    public void ConvertInclusiveLocalDateRangeToUtcRange_UsesExclusiveEndBoundary()
    {
        var timeZone = _service.ResolveRequired("America/Vancouver");

        var range = _service.ConvertInclusiveLocalDateRangeToUtcRange(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 2),
            timeZone
        );

        Assert.Equal(new DateTimeOffset(2026, 7, 1, 7, 0, 0, TimeSpan.Zero), range.StartAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 7, 3, 7, 0, 0, TimeSpan.Zero), range.EndAtUtc);
    }

    [Fact]
    public void ConvertInclusiveLocalDateRangeToUtcRange_WhenSpringDaylightSavingBegins_ReturnsTwentyThreeHours()
    {
        var timeZone = _service.ResolveRequired("America/Vancouver");

        var range = _service.ConvertInclusiveLocalDateRangeToUtcRange(
            new DateOnly(2025, 3, 9),
            new DateOnly(2025, 3, 9),
            timeZone
        );

        Assert.Equal(TimeSpan.FromHours(23), range.EndAtUtc - range.StartAtUtc);
        Assert.Equal(new DateTimeOffset(2025, 3, 9, 8, 0, 0, TimeSpan.Zero), range.StartAtUtc);
        Assert.Equal(new DateTimeOffset(2025, 3, 10, 7, 0, 0, TimeSpan.Zero), range.EndAtUtc);
    }

    [Fact]
    public void ConvertInclusiveLocalDateRangeToUtcRange_WhenAutumnDaylightSavingEnds_ReturnsTwentyFiveHours()
    {
        var timeZone = _service.ResolveRequired("America/Vancouver");

        var range = _service.ConvertInclusiveLocalDateRangeToUtcRange(
            new DateOnly(2025, 11, 2),
            new DateOnly(2025, 11, 2),
            timeZone
        );

        Assert.Equal(TimeSpan.FromHours(25), range.EndAtUtc - range.StartAtUtc);
        Assert.Equal(new DateTimeOffset(2025, 11, 2, 7, 0, 0, TimeSpan.Zero), range.StartAtUtc);
        Assert.Equal(new DateTimeOffset(2025, 11, 3, 8, 0, 0, TimeSpan.Zero), range.EndAtUtc);
    }

    [Fact]
    public void ConvertInclusiveLocalDateRangeToUtcRange_WhenEndDatePrecedesStartDate_Throws()
    {
        var timeZone = _service.ResolveRequired("America/Vancouver");

        var exception = Assert.Throws<ArgumentException>(() =>
            _service.ConvertInclusiveLocalDateRangeToUtcRange(
                new DateOnly(2026, 7, 2),
                new DateOnly(2026, 7, 1),
                timeZone
            )
        );

        Assert.Equal("endDate", exception.ParamName);
    }

    [Fact]
    public void ConvertInclusiveLocalDateRangeToUtcRange_WhenEndDateExceedsSupportedMaximum_Throws()
    {
        var timeZone = _service.ResolveRequired("America/Vancouver");

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.ConvertInclusiveLocalDateRangeToUtcRange(
                TimeZoneDateRangeLimits.MaximumSupportedDate,
                DateOnly.MaxValue,
                timeZone
            )
        );

        Assert.Equal("endDate", exception.ParamName);
        Assert.Contains("9999-12-30", exception.Message);
    }

    [Fact]
    public void ResolveRequired_WhenTimeZoneIsBlank_Throws()
    {
        Assert.Throws<ArgumentException>(() => _service.ResolveRequired(" "));
    }
}
