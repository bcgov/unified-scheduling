using Microsoft.Extensions.Options;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db.Models.Calendar;

namespace Unified.Tests.Calendar.Services;

public class RecurrenceRuleValidatorTests
{
    private readonly ITimeZoneService _timeZoneService = new TimeZoneService();
    private readonly ICalendarTimeZoneResolver _timeZoneResolver = CreateCalendarTimeZoneResolver();
    private readonly IcalNetRecurrenceExpander _expander;
    private readonly IcalNetRecurrenceRuleValidator _validator;

    public RecurrenceRuleValidatorTests()
    {
        _expander = new IcalNetRecurrenceExpander(_timeZoneService, _timeZoneResolver);
        _validator = new IcalNetRecurrenceRuleValidator(_expander, _timeZoneService, _timeZoneResolver);
    }

    [Fact]
    public void Validate_WhenRuleIsBoundedAndWithinLimits_ReturnsValid()
    {
        // Arrange
        var options = CreateOptions();

        // Act
        var result = _validator.Validate(
            "FREQ=DAILY;COUNT=5",
            new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            "America/Vancouver",
            options
        );

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenRuleHasRRulePrefix_ReturnsValid()
    {
        // Arrange
        var options = CreateOptions();

        // Act
        var result = _validator.Validate(
            "RRULE:FREQ=DAILY;COUNT=5",
            new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            "America/Vancouver",
            options
        );

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenRuleIsUnbounded_ReturnsError()
    {
        // Arrange
        var options = CreateOptions();

        // Act
        var result = _validator.Validate(
            "FREQ=DAILY",
            new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            "America/Vancouver",
            options
        );

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("bounded", result.Errors.Single());
    }

    [Fact]
    public void Validator_WhenRuleIsUnboundedAndBoundedRequired_DoesNotEnumerateOccurrences()
    {
        // Arrange
        var expander = new CountingRecurrenceExpander();
        var validator = new IcalNetRecurrenceRuleValidator(expander, _timeZoneService, _timeZoneResolver);

        // Act
        var result = validator.Validate(
            "FREQ=DAILY",
            new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            "America/Vancouver",
            CreateOptions()
        );

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("bounded"));
        Assert.Equal(0, expander.CountWithinCallCount);
        Assert.Equal(0, expander.ExpandWithinCallCount);
    }

    [Fact]
    public void Validate_WhenUntilExceedsMaximumDuration_ReturnsError()
    {
        // Arrange
        var options = CreateOptions();

        // Act
        var result = _validator.Validate(
            "FREQ=DAILY;UNTIL=20270701T000000Z",
            new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            "America/Vancouver",
            options
        );

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("maximum allowed duration"));
    }

    [Fact]
    public void Validator_WhenUntilExceedsMaximumDuration_DoesNotEnumerateOccurrences()
    {
        // Arrange
        var expander = new CountingRecurrenceExpander();
        var validator = new IcalNetRecurrenceRuleValidator(expander, _timeZoneService, _timeZoneResolver);

        // Act
        var result = validator.Validate(
            "FREQ=DAILY;UNTIL=20270701T000000Z",
            new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            "America/Vancouver",
            CreateOptions()
        );

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("maximum allowed duration"));
        Assert.Equal(0, expander.CountWithinCallCount);
        Assert.Equal(0, expander.ExpandWithinCallCount);
    }

    [Fact]
    public void Validate_WhenRuleGeneratesTooManyOccurrences_ReturnsError()
    {
        // Arrange
        var options = CreateOptions(maximumOccurrences: 3);

        // Act
        var result = _validator.Validate(
            "FREQ=DAILY;COUNT=4",
            new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            "America/Vancouver",
            options
        );

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("too many occurrences"));
    }

    [Fact]
    public void Validator_WhenCountExceedsMaximum_DoesNotEnumerateOccurrences()
    {
        // Arrange
        var expander = new CountingRecurrenceExpander();
        var validator = new IcalNetRecurrenceRuleValidator(expander, _timeZoneService, _timeZoneResolver);

        // Act
        var result = validator.Validate(
            "FREQ=SECONDLY;COUNT=10000000",
            new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            "America/Vancouver",
            CreateOptions(maximumOccurrences: 10)
        );

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("too many occurrences"));
        Assert.Equal(0, expander.CountWithinCallCount);
        Assert.Equal(0, expander.ExpandWithinCallCount);
    }

    [Fact]
    public void Validator_WhenDenseUntilRuleExceedsMaximumOccurrences_ReturnsError()
    {
        // Arrange
        var expander = new CountingRecurrenceExpander { CountResult = 11 };
        var validator = new IcalNetRecurrenceRuleValidator(expander, _timeZoneService, _timeZoneResolver);

        // Act
        var result = validator.Validate(
            "FREQ=SECONDLY;UNTIL=20260601T160030Z",
            new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            "UTC",
            CreateOptions(maximumOccurrences: 10)
        );

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("too many occurrences"));
        Assert.Equal(1, expander.CountWithinCallCount);
        Assert.Equal(11, expander.LastStopAfter);
        Assert.Equal(0, expander.ExpandWithinCallCount);
    }

    [Fact]
    public void Validator_WhenRuleGeneratesNoOccurrences_ReturnsValidationError()
    {
        // Arrange
        var options = CreateOptions();

        // Act
        var result = _validator.Validate(
            "FREQ=DAILY;UNTIL=20260531T160000Z",
            new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            "UTC",
            options
        );

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("at least one occurrence"));
    }

    [Fact]
    public void Validator_WhenCountRuleCannotBeSatisfiedWithinMaximumDuration_ReturnsDurationError()
    {
        // Arrange
        var options = CreateOptions(maximumDuration: TimeSpan.FromDays(1));

        // Act
        var result = _validator.Validate(
            "FREQ=YEARLY;COUNT=2",
            new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            "UTC",
            options
        );

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("maximum allowed duration"));
    }

    [Fact]
    public void Validate_WhenRuleIsInvalid_ReturnsError()
    {
        // Arrange
        var options = CreateOptions();

        // Act
        var result = _validator.Validate(
            "NOT-A-RRULE",
            new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            "America/Vancouver",
            options
        );

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("RRULE is invalid"));
    }

    [Theory]
    [InlineData("FREQ=DAILY;COUNT=3", "2025-03-08T09:30:00+00:00", "2025-03-08T10:30:00+00:00", "invalid")]
    [InlineData("FREQ=DAILY;COUNT=3", "2025-11-01T08:30:00+00:00", "2025-11-01T09:30:00+00:00", "ambiguous")]
    public void Validate_WhenRecurrenceExpansionHitsInvalidOrAmbiguousDstLocalTime_ReturnsPredictableError(
        string recurrenceRule,
        string startAtUtc,
        string endAtUtc,
        string expectedMessage
    )
    {
        // Arrange
        var seriesStartAtUtc = DateTimeOffset.Parse(startAtUtc);
        var seriesEndAtUtc = DateTimeOffset.Parse(endAtUtc);

        // Act
        var result = _validator.Validate(
            recurrenceRule,
            seriesStartAtUtc,
            seriesEndAtUtc,
            "America/Vancouver",
            CreateOptions()
        );

        // Assert
        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Contains("RRULE is invalid", error);
        Assert.Contains(expectedMessage, error);
    }

    [Fact]
    public void ExpandWithin_WhenRuleUsesByDay_ReturnsExpectedOccurrenceTimes()
    {
        // Arrange
        var eventSeries = new global::Unified.Db.Models.Calendar.EventSeries
        {
            RecurrenceRule = "FREQ=WEEKLY;COUNT=2;BYDAY=MO",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
        };

        // Act
        var occurrences = _expander.ExpandWithin(
            eventSeries,
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero)
        );

        // Assert
        Assert.Equal(
            [
                new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 8, 16, 0, 0, TimeSpan.Zero),
            ],
            occurrences.Select(x => x.StartAtUtc).ToArray()
        );
        Assert.All(
            occurrences,
            occurrence => Assert.Equal(TimeSpan.FromHours(7), occurrence.EndAtUtc - occurrence.StartAtUtc)
        );
    }

    [Fact]
    public void ExpandWithin_WhenOccurrenceStartsBeforeRangeAndEndsInsideRange_IncludesOccurrence()
    {
        var eventSeries = new EventSeries
        {
            RecurrenceRule = "FREQ=DAILY;COUNT=2",
            TimeZoneId = "UTC",
            StartAtUtc = new DateTimeOffset(2026, 1, 1, 22, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 1, 2, 6, 0, 0, TimeSpan.Zero),
        };

        var occurrences = _expander.ExpandWithin(
            eventSeries,
            new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero)
        );

        Assert.Contains(
            occurrences,
            occurrence =>
                occurrence.StartAtUtc == new DateTimeOffset(2026, 1, 1, 22, 0, 0, TimeSpan.Zero)
                && occurrence.EndAtUtc == new DateTimeOffset(2026, 1, 2, 6, 0, 0, TimeSpan.Zero)
        );
    }

    [Fact]
    public void ExpandWithin_WhenVancouverWeeklyRuleCrossesFromPstToPdt_PreservesLocalWallClockTime()
    {
        // Arrange
        var timeZone = _timeZoneResolver.Resolve("America/Vancouver");
        var eventSeries = new EventSeries
        {
            RecurrenceRule = "FREQ=WEEKLY;COUNT=27",
            TimeZoneId = "America/Vancouver",
            StartAtUtc = new DateTimeOffset(2025, 1, 16, 17, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2025, 1, 17, 1, 0, 0, TimeSpan.Zero),
        };

        // Act
        var occurrences = _expander.ExpandWithin(
            eventSeries,
            eventSeries.StartAtUtc,
            new DateTimeOffset(2025, 7, 31, 0, 0, 0, TimeSpan.Zero)
        );

        // Assert
        var summerOccurrence = occurrences.Single(x =>
            _timeZoneService.ToLocalUnspecified(x.StartAtUtc, timeZone).Date == new DateTime(2025, 7, 17)
        );
        Assert.Equal(new DateTimeOffset(2025, 7, 17, 16, 0, 0, TimeSpan.Zero), summerOccurrence.StartAtUtc);
        Assert.Equal(new DateTimeOffset(2025, 7, 18, 0, 0, 0, TimeSpan.Zero), summerOccurrence.EndAtUtc);
        Assert.Equal(
            new TimeOnly(9, 0),
            TimeOnly.FromDateTime(_timeZoneService.ToLocalUnspecified(summerOccurrence.StartAtUtc, timeZone))
        );
        Assert.Equal(
            new TimeOnly(17, 0),
            TimeOnly.FromDateTime(_timeZoneService.ToLocalUnspecified(summerOccurrence.EndAtUtc!.Value, timeZone))
        );
    }

    [Fact]
    public void ExpandWithin_WhenVancouverWeeklyRuleCrossesFromPdtToPst_PreservesLocalWallClockTime()
    {
        // Arrange
        var timeZone = _timeZoneResolver.Resolve("America/Vancouver");
        var eventSeries = new EventSeries
        {
            RecurrenceRule = "FREQ=WEEKLY;COUNT=17",
            TimeZoneId = "America/Vancouver",
            StartAtUtc = new DateTimeOffset(2025, 7, 16, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2025, 7, 17, 0, 0, 0, TimeSpan.Zero),
        };

        // Act
        var occurrences = _expander.ExpandWithin(
            eventSeries,
            eventSeries.StartAtUtc,
            new DateTimeOffset(2025, 11, 30, 0, 0, 0, TimeSpan.Zero)
        );

        // Assert
        var winterOccurrence = occurrences.Single(x =>
            _timeZoneService.ToLocalUnspecified(x.StartAtUtc, timeZone).Date == new DateTime(2025, 11, 5)
        );
        Assert.Equal(new DateTimeOffset(2025, 11, 5, 17, 0, 0, TimeSpan.Zero), winterOccurrence.StartAtUtc);
        Assert.Equal(new DateTimeOffset(2025, 11, 6, 1, 0, 0, TimeSpan.Zero), winterOccurrence.EndAtUtc);
        Assert.Equal(
            new TimeOnly(9, 0),
            TimeOnly.FromDateTime(_timeZoneService.ToLocalUnspecified(winterOccurrence.StartAtUtc, timeZone))
        );
        Assert.Equal(
            new TimeOnly(17, 0),
            TimeOnly.FromDateTime(_timeZoneService.ToLocalUnspecified(winterOccurrence.EndAtUtc!.Value, timeZone))
        );
    }

    [Fact]
    public void ExpandAllBounded_WhenUntilCrossesFromPdtToPst_IncludesTheFinalLocalOccurrence()
    {
        // Arrange
        var eventSeries = new EventSeries
        {
            RecurrenceRule = "FREQ=WEEKLY;UNTIL=20251105T170000Z",
            TimeZoneId = "America/Vancouver",
            StartAtUtc = new DateTimeOffset(2025, 10, 22, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2025, 10, 23, 0, 0, 0, TimeSpan.Zero),
        };

        // Act
        var occurrences = _expander.ExpandAllBounded(eventSeries, 10);

        // Assert
        Assert.Equal(3, occurrences.Count);
        Assert.Equal(new DateTimeOffset(2025, 11, 5, 17, 0, 0, TimeSpan.Zero), occurrences.Last().StartAtUtc);
    }

    [Fact]
    public void ExpandAllBounded_WhenRuleIsUnbounded_Throws()
    {
        // Arrange
        var eventSeries = new EventSeries
        {
            RecurrenceRule = "FREQ=DAILY",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
        };

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => _expander.ExpandAllBounded(eventSeries, 400));

        // Assert
        Assert.Contains("unbounded", exception.Message);
    }

    [Fact]
    public void ExpandAllBounded_WhenRuleUsesUntil_ReturnsAllOccurrences()
    {
        // Arrange
        var eventSeries = new EventSeries
        {
            RecurrenceRule = "FREQ=DAILY;UNTIL=20260605T160000Z",
            TimeZoneId = "UTC",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
        };

        // Act
        var occurrences = _expander.ExpandAllBounded(eventSeries, 400);

        // Assert
        Assert.Equal(5, occurrences.Count);
        Assert.Equal(new DateTimeOffset(2026, 6, 5, 16, 0, 0, TimeSpan.Zero), occurrences.Last().StartAtUtc);
    }

    [Fact]
    public void CountWithin_WhenStopAfterReached_ReturnsStopAfterCount()
    {
        // Arrange
        var eventSeries = new EventSeries
        {
            RecurrenceRule = "FREQ=SECONDLY;UNTIL=20260601T160030Z",
            TimeZoneId = "UTC",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 17, 0, 0, TimeSpan.Zero),
        };

        // Act
        var result = _expander.CountWithin(
            eventSeries,
            eventSeries.StartAtUtc,
            eventSeries.StartAtUtc.AddMinutes(1),
            stopAfter: 11
        );

        // Assert
        Assert.Equal(11, result);
    }

    [Fact]
    public void CountWithin_WhenFewerOccurrencesExist_ReturnsCount()
    {
        // Arrange
        var eventSeries = new EventSeries
        {
            RecurrenceRule = "FREQ=DAILY;COUNT=3",
            TimeZoneId = "UTC",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 17, 0, 0, TimeSpan.Zero),
        };

        // Act
        var result = _expander.CountWithin(
            eventSeries,
            eventSeries.StartAtUtc,
            eventSeries.StartAtUtc.AddDays(10),
            stopAfter: 11
        );

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public void CountWithin_WhenStopAfterIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var eventSeries = new EventSeries
        {
            RecurrenceRule = "FREQ=DAILY;COUNT=3",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 17, 0, 0, TimeSpan.Zero),
        };

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _expander.CountWithin(eventSeries, eventSeries.StartAtUtc, eventSeries.StartAtUtc.AddDays(10), stopAfter: 0)
        );
    }

    [Fact]
    public void ExpandAllBounded_WhenOccurrenceCountExceedsMaximum_Throws()
    {
        // Arrange
        var eventSeries = new EventSeries
        {
            RecurrenceRule = "FREQ=DAILY;COUNT=4",
            TimeZoneId = "UTC",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 17, 0, 0, TimeSpan.Zero),
        };

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => _expander.ExpandAllBounded(eventSeries, 3));

        // Assert
        Assert.Contains("too many occurrences", exception.Message);
    }

    private static RecurrenceValidationOptions CreateOptions(
        int maximumOccurrences = 400,
        TimeSpan? maximumDuration = null
    ) =>
        new()
        {
            MaximumDuration = maximumDuration ?? TimeSpan.FromDays(365),
            MaximumOccurrences = maximumOccurrences,
            RequireBoundedRule = true,
        };

    private static CalendarTimeZoneResolver CreateCalendarTimeZoneResolver() =>
        new(
            Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "America/Vancouver" }),
            new TimeZoneService()
        );

    private sealed class CountingRecurrenceExpander : IRecurrenceExpander
    {
        public int ExpandWithinCallCount { get; private set; }

        public int CountWithinCallCount { get; private set; }

        public int? LastStopAfter { get; private set; }

        public int CountResult { get; init; } = 1;

        public IReadOnlyCollection<SeriesEntry> ExpandWithin(
            EventSeries eventSeries,
            DateTimeOffset rangeStartAtUtc,
            DateTimeOffset rangeEndAtUtc
        )
        {
            ExpandWithinCallCount++;
            return [];
        }

        public IReadOnlyCollection<SeriesEntry> ExpandAllBounded(EventSeries eventSeries, int maximumOccurrences) => [];

        public int CountWithin(
            EventSeries eventSeries,
            DateTimeOffset rangeStartAtUtc,
            DateTimeOffset rangeEndAtUtc,
            int stopAfter
        )
        {
            CountWithinCallCount++;
            LastStopAfter = stopAfter;
            return CountResult;
        }
    }
}
