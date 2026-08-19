using Microsoft.Extensions.Options;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Time;

namespace Unified.Tests.Calendar.Services;

public class CalendarTimeZoneResolverTests
{
    [Fact]
    public void Resolve_WhenRequestedTimeZoneIsMissing_UsesFallbackTimeZone()
    {
        var resolver = CreateResolver();

        var timeZone = resolver.Resolve(null, "America/Toronto");

        Assert.Equal("America/Toronto", timeZone.Id);
    }

    [Fact]
    public void Resolve_WhenRequestedAndFallbackTimeZonesAreMissing_UsesConfiguredDefaultTimeZone()
    {
        var resolver = CreateResolver("America/Toronto");

        var timeZone = resolver.Resolve(null);

        Assert.Equal("America/Toronto", timeZone.Id);
    }

    [Fact]
    public void Resolve_WhenRequestedTimeZoneIsPresent_PrefersItOverFallbackTimeZone()
    {
        var resolver = CreateResolver();

        var timeZone = resolver.Resolve("America/Edmonton", "America/Toronto");

        Assert.Equal("America/Edmonton", timeZone.Id);
    }

    [Fact]
    public void Resolve_WhenRequestedTimeZoneIsInvalid_Throws()
    {
        var resolver = CreateResolver();

        Assert.Throws<TimeZoneNotFoundException>(() => resolver.Resolve("Not/AZone", "America/Toronto"));
    }

    [Fact]
    public void Resolve_WhenFallbackTimeZoneIsInvalid_Throws()
    {
        var resolver = CreateResolver();

        Assert.Throws<TimeZoneNotFoundException>(() => resolver.Resolve(null, "Not/AZone"));
    }

    [Fact]
    public void Resolve_WhenConfiguredDefaultTimeZoneIsInvalid_Throws()
    {
        var resolver = CreateResolver("Not/AZone");

        Assert.Throws<TimeZoneNotFoundException>(() => resolver.Resolve(null));
    }

    private static CalendarTimeZoneResolver CreateResolver(string defaultTimeZoneId = "America/Vancouver") =>
        new(
            Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = defaultTimeZoneId }),
            new TimeZoneService()
        );
}
