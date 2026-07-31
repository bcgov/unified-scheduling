using Unified.Common.Time;

namespace Unified.Tests.Common.Time;

public class TimeZoneServiceTests
{
    private readonly TimeZoneService _service = new();

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
    [InlineData(2026, 3, 8, 2, 30, "invalid")]
    [InlineData(2026, 11, 1, 1, 30, "ambiguous")]
    public void ToUtcInstant_WhenLocalTimeIsInvalidOrAmbiguous_Throws(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        string expectedMessage
    )
    {
        var timeZone = _service.ResolveRequired("America/Vancouver");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _service.ToUtcInstant(new DateTime(year, month, day, hour, minute, 0), timeZone)
        );

        Assert.Contains(expectedMessage, exception.Message);
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
            new DateOnly(2026, 3, 8),
            new DateOnly(2026, 3, 8),
            timeZone
        );

        Assert.Equal(TimeSpan.FromHours(23), range.EndAtUtc - range.StartAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 3, 8, 8, 0, 0, TimeSpan.Zero), range.StartAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 3, 9, 7, 0, 0, TimeSpan.Zero), range.EndAtUtc);
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
