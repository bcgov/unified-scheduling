using Unified.Common.Time;

namespace Unified.Tests.Common.Time;

public class TimeZoneServiceTests
{
    private readonly ITimeZoneService _service = new TimeZoneService();

    [Theory]
    [InlineData(7)]
    [InlineData(12, Skip = "Temporary: NuGet mapper does not yet support B.C.'s updated IANA timezone mapping.")]
    public void Vancouver_AfterPermanentPacificTime_UsesUtcMinusSeven(int month)
    {
        var zone = _service.ResolveRequired("America/Vancouver");
        var instant = new DateTimeOffset(2026, month, 1, 16, 30, 0, TimeSpan.Zero);

        Assert.Equal(TimeSpan.FromHours(-7), zone.GetUtcOffset(instant));
        Assert.Equal(new DateTime(2026, month, 1, 9, 30, 0), _service.ToLocalUnspecified(instant, zone));
        Assert.Equal(instant, _service.ToUtcInstant(new DateTime(2026, month, 1, 9, 30, 0), zone));
    }

    [Theory]
    [InlineData("America/Vancouver", 2025, 1, -8)]
    [InlineData("America/Vancouver", 2025, 7, -7)]
    [InlineData("America/Vancouver", 2026, 1, -8)]
    [InlineData("America/Dawson_Creek", 2026, 11, -7)]
    [InlineData("America/Dawson_Creek", 2027, 1, -7)]
    public void ResolveOrUtc_WhenTimeZoneExists_ReturnsTimeZone(
        string timeZoneId,
        int year,
        int month,
        int expectedOffsetHours
    )
    {
        var result = _service.ResolveOrUtc($" {timeZoneId} ");
        var requiredResult = _service.ResolveRequired(timeZoneId);
        var instant = new DateTimeOffset(year, month, 2, 16, 0, 0, TimeSpan.Zero);

        Assert.Equal(timeZoneId, result.Id);
        Assert.Equal(timeZoneId, requiredResult.Id);
        Assert.Equal(TimeSpan.FromHours(expectedOffsetHours), result.GetUtcOffset(instant));
        Assert.Equal(TimeSpan.FromHours(expectedOffsetHours), requiredResult.GetUtcOffset(instant));
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

    [Theory]
    [InlineData(2026, 7, 1, 9, 30, 16, 30)]
    [InlineData(2026, 11, 1, 1, 30, 8, 30)]
    [InlineData(2027, 1, 1, 9, 0, 16, 0)]
    public void ToUtcInstant_WhenTimeZoneHasPermanentOffset_ReturnsUtcInstant(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int utcHour,
        int utcMinute
    )
    {
        // Dawson Creek has long observed UTC-7, independently of recent Vancouver rule updates.
        var timeZone = _service.ResolveRequired("America/Dawson_Creek");

        var result = _service.ToUtcInstant(new DateTime(year, month, day, hour, minute, 0), timeZone);

        Assert.Equal(new DateTimeOffset(year, month, day, utcHour, utcMinute, 0, TimeSpan.Zero), result);
    }

    [Theory]
    [InlineData(2025, 3, 9, 2, 30, "invalid")]
    [InlineData(2025, 11, 2, 1, 30, "ambiguous")]
    [InlineData(2026, 3, 8, 2, 30, "invalid")]
    public void ToUtcInstant_WhenLocalTimeIsInvalidOrAmbiguous_Throws(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        string expectedMessage
    )
    {
        // Exercise DST rejection without Vancouver's platform-dependent historical rule representation.
        var timeZone = _service.ResolveRequired("America/Los_Angeles");

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

    [Theory]
    [InlineData("America/Los_Angeles", 2026, 3, 8, 23, "2026-03-08T08:00:00Z", "2026-03-09T07:00:00Z")]
    [InlineData("America/Los_Angeles", 2025, 11, 2, 25, "2025-11-02T07:00:00Z", "2025-11-03T08:00:00Z")]
    [InlineData("America/Dawson_Creek", 2026, 11, 1, 24, "2026-11-01T07:00:00Z", "2026-11-02T07:00:00Z")]
    public void ConvertInclusiveLocalDateRangeToUtcRange_WhenZoneObservesDstOrPermanentOffset_ReturnsExpectedDayLength(
        string timeZoneId,
        int year,
        int month,
        int day,
        int hours,
        string expectedStart,
        string expectedEnd
    )
    {
        var timeZone = _service.ResolveRequired(timeZoneId);

        var range = _service.ConvertInclusiveLocalDateRangeToUtcRange(
            new DateOnly(year, month, day),
            new DateOnly(year, month, day),
            timeZone
        );

        Assert.Equal(TimeSpan.FromHours(hours), range.EndAtUtc - range.StartAtUtc);
        Assert.Equal(DateTimeOffset.Parse(expectedStart), range.StartAtUtc);
        Assert.Equal(DateTimeOffset.Parse(expectedEnd), range.EndAtUtc);
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

    [Fact]
    public void ResolveRequired_WhenWindowsAliasIsSupported_NormalizesToIanaId()
    {
        var result = _service.ResolveRequired("Pacific Standard Time");

        Assert.Equal(TimeZoneService.NormalizeTimeZoneId("Pacific Standard Time"), result.Id);
        Assert.NotEqual("Pacific Standard Time", result.Id);
    }
}
