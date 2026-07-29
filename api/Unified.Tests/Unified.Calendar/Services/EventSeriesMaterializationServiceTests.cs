using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.Calendar.Services;

public class EventSeriesMaterializationServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private UnifiedDbContext _dbContext = null!;
    private EventSeriesMaterializationService _service = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.CreateFunction("now", () => DateTimeOffset.UtcNow.ToString("O"));
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(_connection).Options;
        _dbContext = new SqliteTestUnifiedDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        _dbContext.EventTypes.Add(CreateEventType(CalendarEventTypeCodes.General));
        _dbContext.EventTypes.Add(CreateEventType("series-type"));
        _dbContext.EventStatusTypes.Add(CreateStatusType(CalendarEventStatusTypeCodes.Draft));
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var timeZoneService = new TimeZoneService();
        var timeZoneResolver = CreateCalendarTimeZoneResolver();
        var recurrenceExpander = new IcalNetRecurrenceExpander(timeZoneService, timeZoneResolver);
        _service = new EventSeriesMaterializationService(
            _dbContext,
            new IcalNetRecurrenceRuleValidator(recurrenceExpander, timeZoneService, timeZoneResolver),
            recurrenceExpander
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task MaterializeAsync_WhenSeriesHasMultipleOccurrences_UsesProvidedContext()
    {
        // Arrange
        var eventSeries = new EventSeries
        {
            Title = "Series",
            RecurrenceRule = "FREQ=DAILY;COUNT=3",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            EventTypeCode = CalendarEventTypeCodes.General,
            StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
        };
        _dbContext.EventSeries.Add(eventSeries);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var handler = new CountingMaterializationHandler();

        // Act
        await MaterializeAsync(eventSeries, CreateOptions(), handler, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, handler.CreatedContextIds.Count);
        Assert.All(handler.CreatedContextIds, id => Assert.Equal(handler.ContextId, id));
        Assert.Equal(3, await _dbContext.Events.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MaterializeAsync_WhenRuleExceedsMaximumDuration_FailsValidationBeforeMaterialization()
    {
        // Arrange
        var eventSeries = await AddSeriesAsync("FREQ=DAILY;COUNT=3");
        var handler = new CountingMaterializationHandler();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MaterializeAsync(
                eventSeries,
                CreateOptions(maximumDuration: TimeSpan.FromDays(1)),
                handler,
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Contains("maximum allowed duration", exception.Message);
        Assert.Empty(_dbContext.Events);
    }

    [Fact]
    public async Task MaterializeAsync_WhenRuleExceedsMaximumOccurrences_FailsValidationBeforeMaterialization()
    {
        // Arrange
        var eventSeries = await AddSeriesAsync("FREQ=DAILY;COUNT=4");
        var handler = new CountingMaterializationHandler();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MaterializeAsync(
                eventSeries,
                CreateOptions(maximumOccurrences: 3),
                handler,
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Contains("too many occurrences", exception.Message);
        Assert.Empty(_dbContext.Events);
    }

    [Fact]
    public async Task MaterializeAsync_WhenRuleIsValidAndBounded_MaterializesAllOccurrences()
    {
        // Arrange
        var eventSeries = await AddSeriesAsync("FREQ=DAILY;COUNT=5");
        var handler = new CountingMaterializationHandler();

        // Act
        await MaterializeAsync(eventSeries, CreateOptions(), handler, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(5, await _dbContext.Events.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MaterializeAsync_WhenCreatingEvents_UsesSeriesMappingWithExpectedOverrides()
    {
        // Arrange
        var eventSeries = new EventSeries
        {
            Title = "Series",
            Description = " Description ",
            Notes = "Notes",
            Color = "#123456",
            RecurrenceRule = "FREQ=DAILY;COUNT=1",
            TimeZoneId = "UTC",
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            AllDay = true,
            EventTypeCode = "series-type",
            StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
            CancelledAt = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            CancellationReason = "series cancelled",
        };
        _dbContext.EventSeries.Add(eventSeries);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var handler = new CountingMaterializationHandler();

        // Act
        await MaterializeAsync(eventSeries, CreateOptions(), handler, TestContext.Current.CancellationToken);

        // Assert
        var materializedEvent = await _dbContext.Events.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(eventSeries.Id, materializedEvent.EventSeriesId);
        Assert.Equal(eventSeries.Title, materializedEvent.Title);
        Assert.Equal(eventSeries.Description, materializedEvent.Description);
        Assert.Equal(eventSeries.Notes, materializedEvent.Notes);
        Assert.Equal(eventSeries.Color, materializedEvent.Color);
        Assert.Equal(eventSeries.TimeZoneId, materializedEvent.TimeZoneId);
        Assert.Equal(eventSeries.AllDay, materializedEvent.AllDay);
        Assert.Equal(eventSeries.StatusTypeCode, materializedEvent.StatusTypeCode);
        Assert.Equal(eventSeries.LocationId, materializedEvent.LocationId);
        Assert.Equal(handler.EventTypeCode, materializedEvent.EventTypeCode);
        Assert.Equal(handler.SourceModule, materializedEvent.SourceModule);
        Assert.Equal(eventSeries.StartAtUtc, materializedEvent.StartAtUtc);
        Assert.Equal(eventSeries.EndAtUtc, materializedEvent.EndAtUtc);
        Assert.Equal(eventSeries.StartAtUtc, materializedEvent.SeriesStartAtUtc);
        Assert.Equal(eventSeries.EndAtUtc, materializedEvent.SeriesEndAtUtc);
        Assert.False(materializedEvent.IsException);
        Assert.Null(materializedEvent.CancelledAt);
        Assert.Null(materializedEvent.CancelledByUserId);
        Assert.Null(materializedEvent.CancellationReason);
    }

    [Fact]
    public async Task MaterializeAsync_WhenRuleIsUnbounded_FailsValidationBeforeMaterialization()
    {
        // Arrange
        var eventSeries = await AddSeriesAsync("FREQ=DAILY");
        var handler = new CountingMaterializationHandler();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MaterializeAsync(eventSeries, CreateOptions(), handler, TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Contains("bounded", exception.Message);
        Assert.Empty(_dbContext.Events);
    }

    [Fact]
    public async Task RegenerateDraftSeriesAsync_WhenRegenerationIsAllowed_DeletesAndCreatesSeriesEvents()
    {
        // Arrange
        var eventSeries = await AddSeriesAsync("FREQ=DAILY;COUNT=3");
        var handler = new CountingMaterializationHandler();
        await MaterializeAsync(eventSeries, CreateOptions(), handler, TestContext.Current.CancellationToken);

        var exceptionEvent = await _dbContext
            .Events.OrderBy(x => x.SeriesStartAtUtc)
            .FirstAsync(x => x.EventSeriesId == eventSeries.Id, TestContext.Current.CancellationToken);
        exceptionEvent.StartAtUtc = exceptionEvent.StartAtUtc.AddHours(1);
        CalendarEventExceptionHelper.UpdateExceptionFlag(exceptionEvent);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var originalEventIds = await _dbContext
            .Events.Where(x => x.EventSeriesId == eventSeries.Id)
            .Select(x => x.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        eventSeries.RecurrenceRule = "FREQ=DAILY;COUNT=2";

        // Act
        await RegenerateDraftSeriesAsync(eventSeries, CreateOptions(), handler, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(originalEventIds.Order().ToArray(), handler.DeletedEventIds.Order().ToArray());
        Assert.False(
            await _dbContext.Events.AnyAsync(
                x => originalEventIds.Contains(x.Id),
                TestContext.Current.CancellationToken
            )
        );

        var recreatedEvents = await _dbContext
            .Events.Where(x => x.EventSeriesId == eventSeries.Id)
            .OrderBy(x => x.SeriesStartAtUtc)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, recreatedEvents.Count);
        Assert.All(recreatedEvents, x => Assert.False(x.IsException));
    }

    [Fact]
    public async Task MaterializeAsync_WhenSeriesIsAlreadyMaterialized_FailsBeforeCreatingMoreEvents()
    {
        var eventSeries = await AddSeriesAsync("FREQ=DAILY;COUNT=1");
        var handler = new CountingMaterializationHandler();
        await MaterializeAsync(eventSeries, CreateOptions(), handler, TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MaterializeAsync(eventSeries, CreateOptions(), handler, TestContext.Current.CancellationToken)
        );

        Assert.Contains("already been materialized", exception.Message);
        Assert.Single(_dbContext.Events);
    }

    [Fact]
    public async Task MaterializeAsync_WhenSeriesHasNoEndTime_FailsBeforeCreatingEvents()
    {
        var eventSeries = await AddSeriesAsync("FREQ=DAILY;COUNT=1");
        eventSeries.EndAtUtc = null;
        var handler = new CountingMaterializationHandler();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MaterializeAsync(eventSeries, CreateOptions(), handler, TestContext.Current.CancellationToken)
        );

        Assert.Contains("end date and time", exception.Message);
        Assert.Empty(_dbContext.Events);
    }

    private async Task<EventSeries> AddSeriesAsync(string recurrenceRule)
    {
        var eventSeries = new EventSeries
        {
            Title = "Series",
            RecurrenceRule = recurrenceRule,
            StartAtUtc = new DateTimeOffset(2026, 6, 1, 16, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            EventTypeCode = CalendarEventTypeCodes.General,
            StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
        };
        _dbContext.EventSeries.Add(eventSeries);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return eventSeries;
    }

    private async Task MaterializeAsync(
        EventSeries eventSeries,
        RecurrenceValidationOptions options,
        CountingMaterializationHandler handler,
        CancellationToken cancellationToken
    )
    {
        await _service.MaterializeAsync(
            eventSeries,
            options,
            handler,
            new CountingContext(handler.ContextId),
            cancellationToken
        );
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RegenerateDraftSeriesAsync(
        EventSeries eventSeries,
        RecurrenceValidationOptions options,
        CountingMaterializationHandler handler,
        CancellationToken cancellationToken
    )
    {
        await _service.RegenerateDraftSeriesAsync(
            eventSeries,
            options,
            handler,
            new CountingContext(handler.ContextId),
            cancellationToken
        );
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static RecurrenceValidationOptions CreateOptions(
        TimeSpan? maximumDuration = null,
        int maximumOccurrences = 400
    ) =>
        new()
        {
            MaximumDuration = maximumDuration ?? TimeSpan.FromDays(365),
            MaximumOccurrences = maximumOccurrences,
            RequireBoundedRule = true,
        };

    private static EventType CreateEventType(string code) =>
        new()
        {
            Code = code,
            Description = code,
            EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

    private static EventStatusType CreateStatusType(string code) =>
        new()
        {
            Code = code,
            Description = code,
            EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

    private static CalendarTimeZoneResolver CreateCalendarTimeZoneResolver() =>
        new(
            Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "America/Vancouver" }),
            new TimeZoneService()
        );

    private sealed class CountingMaterializationHandler : IEventSeriesMaterializationHandler<CountingContext>
    {
        public string SourceModule => CalendarConstants.SourceModule;

        public string EventTypeCode => CalendarEventTypeCodes.General;

        public Guid ContextId { get; } = Guid.NewGuid();

        public List<Guid> CreatedContextIds { get; } = [];

        public List<int> DeletedEventIds { get; } = [];

        public Task OnMaterializedEventCreatedAsync(
            EventSeries eventSeries,
            Event eventEntity,
            SeriesEntry occurrence,
            CountingContext context,
            CancellationToken cancellationToken
        )
        {
            CreatedContextIds.Add(context.Id);
            return Task.CompletedTask;
        }

        public Task OnMaterializedEventsDeletingAsync(
            EventSeries eventSeries,
            IReadOnlyCollection<Event> existingEvents,
            CountingContext context,
            CancellationToken cancellationToken
        )
        {
            DeletedEventIds.AddRange(existingEvents.Select(eventEntity => eventEntity.Id));
            return Task.CompletedTask;
        }
    }

    private sealed record CountingContext(Guid Id);
}
