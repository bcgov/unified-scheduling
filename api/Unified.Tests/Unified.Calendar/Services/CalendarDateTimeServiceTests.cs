using Microsoft.Extensions.Options;
using Unified.Calendar.Options;
using Unified.Calendar.Services;

namespace Unified.Tests.Calendar.Services;

public class CalendarDateTimeServiceTests
{
    private readonly CalendarDateTimeService _service = CreateService();

    [Fact]
    public void ConvertLocalDateRangeToUtcRange_WhenVancouverDateIsInPdt_UsesLocalMidnightBoundaries()
    {
        // Arrange
        var timeZone = _service.ResolveTimeZone("America/Vancouver");

        // Act
        var range = _service.ConvertInclusiveLocalDateRangeToUtcRange(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 2),
            timeZone
        );

        // Assert
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 7, 0, 0, TimeSpan.Zero), range.StartAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 7, 3, 7, 0, 0, TimeSpan.Zero), range.EndAtUtc);
    }

    [Fact]
    public void ConvertInclusiveLocalDateRangeToUtcRange_WhenStartAndEndAreSameDate_IncludesWholeDay()
    {
        // Arrange
        var timeZone = _service.ResolveTimeZone("America/Vancouver");

        // Act
        var range = _service.ConvertInclusiveLocalDateRangeToUtcRange(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 1),
            timeZone
        );

        // Assert
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 7, 0, 0, TimeSpan.Zero), range.StartAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 7, 2, 7, 0, 0, TimeSpan.Zero), range.EndAtUtc);
    }

    [Theory]
    [InlineData(2026, 3, 8, 2, 30, "invalid")]
    [InlineData(2026, 11, 1, 1, 30, "ambiguous")]
    public void ToUtcInstant_WhenLocalTimeIsInvalidOrAmbiguous_ThrowsInvalidOperationException(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        string expectedMessage
    )
    {
        // Arrange
        var timeZone = _service.ResolveTimeZone("America/Vancouver");
        var localTime = new DateTime(year, month, day, hour, minute, 0);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => _service.ToUtcInstant(localTime, timeZone));

        // Assert
        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public void ResolveTimeZone_WhenRequestedTimeZoneIsMissing_UsesFallbackTimeZone()
    {
        // Act
        var timeZone = _service.ResolveTimeZone(null, "America/Toronto");

        // Assert
        Assert.Equal("America/Toronto", timeZone.Id);
    }

    [Fact]
    public void ResolveTimeZone_WhenRequestedAndFallbackTimeZonesAreMissing_UsesConfiguredDefaultTimeZone()
    {
        // Arrange
        var service = CreateService("America/Toronto");

        // Act
        var timeZone = service.ResolveTimeZone(null, null);

        // Assert
        Assert.Equal("America/Toronto", timeZone.Id);
    }

    private static CalendarDateTimeService CreateService(string defaultTimeZoneId = "America/Vancouver") =>
        new(Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = defaultTimeZoneId }));
}
