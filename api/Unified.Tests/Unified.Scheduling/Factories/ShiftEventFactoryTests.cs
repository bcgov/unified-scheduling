using FluentValidation;
using Microsoft.Extensions.Options;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Factories;
using Unified.Scheduling.Models;
using Unified.Scheduling.Options;

namespace Unified.Tests.Scheduling.Factories;

public sealed class ShiftEventFactoryTests
{
    private readonly ShiftEventFactory factory = new(
        Options.Create(new WorkingHoursOptions { DefaultLunchMinutes = 30 })
    );

    [Theory]
    [InlineData(null, null, 30, 0)]
    [InlineData(null, 30, 30, 30)]
    [InlineData(0, 0, 0, 0)]
    public void Create_ResolvesDefaultsAndAcceptsValidLunchForEntriesAndSeries(
        int? available,
        int? worked,
        int expectedAvailable,
        int expectedWorked
    )
    {
        var entry = factory.CreateEntry(
            new ShiftEntryRequest { LunchAvailableMinutes = available, WorkedLunchMinutes = worked },
            new Event()
        );
        var series = factory.CreateSeries(
            new ShiftSeriesRequest { LunchAvailableMinutes = available, WorkedLunchMinutes = worked },
            new EventSeries()
        );
        Assert.Equal(expectedAvailable, entry.LunchAvailableMinutes);
        Assert.Equal(expectedWorked, entry.WorkedLunchMinutes);
        Assert.Equal(entry.LunchAvailableMinutes, series.LunchAvailableMinutes);
        Assert.Equal(entry.WorkedLunchMinutes, series.WorkedLunchMinutes);
    }

    [Theory]
    [InlineData(null, 31)]
    [InlineData(null, -1)]
    [InlineData(-1, 0)]
    [InlineData(61, 0)]
    public void Create_InvalidEffectiveLunch_IsRejectedForEntriesAndSeries(int? available, int? worked)
    {
        Assert.Throws<ValidationException>(() =>
            factory.CreateEntry(
                new ShiftEntryRequest { LunchAvailableMinutes = available, WorkedLunchMinutes = worked },
                new Event()
            )
        );
        Assert.Throws<ValidationException>(() =>
            factory.CreateSeries(
                new ShiftSeriesRequest { LunchAvailableMinutes = available, WorkedLunchMinutes = worked },
                new EventSeries()
            )
        );
    }

    [Theory]
    [InlineData(null, null, 30, 15)]
    [InlineData(null, 0, 30, 0)]
    [InlineData(45, null, 45, 15)]
    [InlineData(0, 0, 0, 0)]
    public void Update_PreservesOmittedLunchAndAcceptsExplicitZero(
        int? available,
        int? worked,
        int expectedAvailable,
        int expectedWorked
    )
    {
        var entry = new ShiftEntry { LunchAvailableMinutes = 30, WorkedLunchMinutes = 15 };
        var series = new ShiftSeries { LunchAvailableMinutes = 30, WorkedLunchMinutes = 15 };
        factory.UpdateEntry(
            new ShiftEntryRequest { LunchAvailableMinutes = available, WorkedLunchMinutes = worked },
            entry
        );
        factory.UpdateSeries(
            new ShiftSeriesRequest { LunchAvailableMinutes = available, WorkedLunchMinutes = worked },
            series
        );
        Assert.Equal(expectedAvailable, entry.LunchAvailableMinutes);
        Assert.Equal(expectedWorked, entry.WorkedLunchMinutes);
        Assert.Equal(entry.LunchAvailableMinutes, series.LunchAvailableMinutes);
        Assert.Equal(entry.WorkedLunchMinutes, series.WorkedLunchMinutes);
    }

    [Theory]
    [InlineData(null, 31)]
    [InlineData(10, null)]
    [InlineData(null, -1)]
    public void Update_InvalidEffectiveLunch_RejectsBeforeMutatingEitherEntity(int? available, int? worked)
    {
        var entry = new ShiftEntry { LunchAvailableMinutes = 30, WorkedLunchMinutes = 15 };
        var series = new ShiftSeries { LunchAvailableMinutes = 30, WorkedLunchMinutes = 15 };
        Assert.Throws<ValidationException>(() =>
            factory.UpdateEntry(
                new ShiftEntryRequest { LunchAvailableMinutes = available, WorkedLunchMinutes = worked },
                entry
            )
        );
        Assert.Throws<ValidationException>(() =>
            factory.UpdateSeries(
                new ShiftSeriesRequest { LunchAvailableMinutes = available, WorkedLunchMinutes = worked },
                series
            )
        );
        Assert.Equal(30, entry.LunchAvailableMinutes);
        Assert.Equal(15, entry.WorkedLunchMinutes);
        Assert.Equal(30, series.LunchAvailableMinutes);
        Assert.Equal(15, series.WorkedLunchMinutes);
    }

    [Fact]
    public void Update_LegacyUnknownLunch_PreservesUnknownUntilExplicitlySupplied()
    {
        var entry = new ShiftEntry();
        var series = new ShiftSeries();
        factory.UpdateEntry(new ShiftEntryRequest(), entry);
        factory.UpdateSeries(new ShiftSeriesRequest(), series);
        Assert.Null(entry.LunchAvailableMinutes);
        Assert.Null(entry.WorkedLunchMinutes);
        Assert.Null(series.LunchAvailableMinutes);
        Assert.Null(series.WorkedLunchMinutes);

        factory.UpdateEntry(new ShiftEntryRequest { LunchAvailableMinutes = 0, WorkedLunchMinutes = 0 }, entry);
        factory.UpdateSeries(new ShiftSeriesRequest { LunchAvailableMinutes = 0, WorkedLunchMinutes = 0 }, series);
        Assert.Equal(0, entry.LunchAvailableMinutes);
        Assert.Equal(0, entry.WorkedLunchMinutes);
        Assert.Equal(0, series.LunchAvailableMinutes);
        Assert.Equal(0, series.WorkedLunchMinutes);
    }
}
