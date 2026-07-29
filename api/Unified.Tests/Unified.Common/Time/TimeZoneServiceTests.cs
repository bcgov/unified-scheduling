using Unified.Common.Time;

namespace Unified.Tests.Common.Time;

public class TimeZoneServiceTests
{
    private readonly TimeZoneService _service = new();

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
    public void ResolveRequired_WhenTimeZoneIsBlank_Throws()
    {
        Assert.Throws<ArgumentException>(() => _service.ResolveRequired(" "));
    }
}
