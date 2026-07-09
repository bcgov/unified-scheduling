using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
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
        _dbContext.EventStatusTypes.Add(CreateStatusType(CalendarEventStatusTypeCodes.Cancelled));
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var calendarDateTimeService = CreateCalendarDateTimeService();
        var recurrenceExpander = new IcalNetRecurrenceExpander(calendarDateTimeService);
        _service = new EventSeriesMaterializationService(
            _dbContext,
            new IcalNetRecurrenceRuleValidator(recurrenceExpander, calendarDateTimeService),
            recurrenceExpander
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task MaterializeAsync_WhenSeriesHasMultipleOccurrences_CreatesHandlerContextOnce()
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
        var result = await _service.MaterializeAsync(
            eventSeries,
            CreateOptions(),
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(1, handler.ContextCreateCount);
        Assert.Equal(3, handler.CreatedContextIds.Count);
        Assert.All(handler.CreatedContextIds, id => Assert.Equal(handler.ContextId, id));
        Assert.Equal(3, result.CreatedCount);
        Assert.Equal(3, result.CreatedEventIds.Count);
    }

    [Fact]
    public async Task MaterializeAsync_WhenRuleExceedsMaximumDuration_FailsValidationBeforeMaterialization()
    {
        // Arrange
        var eventSeries = await AddSeriesAsync("FREQ=DAILY;COUNT=3");
        var handler = new CountingMaterializationHandler();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.MaterializeAsync(
                eventSeries,
                CreateOptions(maximumDuration: TimeSpan.FromDays(1)),
                handler,
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Contains("maximum allowed duration", exception.Message);
        Assert.Equal(0, handler.ContextCreateCount);
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
            _service.MaterializeAsync(
                eventSeries,
                CreateOptions(maximumOccurrences: 3),
                handler,
                TestContext.Current.CancellationToken
            )
        );

        // Assert
        Assert.Contains("too many occurrences", exception.Message);
        Assert.Equal(0, handler.ContextCreateCount);
        Assert.Empty(_dbContext.Events);
    }

    [Fact]
    public async Task MaterializeAsync_WhenRuleIsValidAndBounded_MaterializesAllOccurrences()
    {
        // Arrange
        var eventSeries = await AddSeriesAsync("FREQ=DAILY;COUNT=5");
        var handler = new CountingMaterializationHandler();

        // Act
        var result = await _service.MaterializeAsync(
            eventSeries,
            CreateOptions(),
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(5, result.CreatedCount);
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
        await _service.MaterializeAsync(eventSeries, CreateOptions(), handler, TestContext.Current.CancellationToken);

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
            _service.MaterializeAsync(eventSeries, CreateOptions(), handler, TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Contains("bounded", exception.Message);
        Assert.Equal(0, handler.ContextCreateCount);
        Assert.Empty(_dbContext.Events);
    }

    [Fact]
    public async Task RecreateAsync_WhenAllowed_CreatesNewEventsWithoutReusingExistingIds()
    {
        // Arrange
        var eventSeries = await AddSeriesAsync("FREQ=DAILY;COUNT=2");
        var handler = new CountingMaterializationHandler();
        await _service.MaterializeAsync(eventSeries, CreateOptions(), handler, TestContext.Current.CancellationToken);
        handler.CreatedContextIds.Clear();

        var originalEventIds = await _dbContext
            .Events.Where(x => x.EventSeriesId == eventSeries.Id)
            .Select(x => x.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        var exceptionEvent = await _dbContext
            .Events.OrderBy(x => x.SeriesStartAtUtc)
            .FirstAsync(x => x.EventSeriesId == eventSeries.Id, TestContext.Current.CancellationToken);
        exceptionEvent.Title = "Custom occurrence";
        exceptionEvent.StartAtUtc = exceptionEvent.StartAtUtc.AddHours(1);
        CalendarEventExceptionHelper.UpdateExceptionFlag(exceptionEvent);
        eventSeries.Title = "Regenerated series";
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.RecreateAsync(
            eventSeries,
            CreateOptions(),
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(2, result.CreatedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(2, result.RemovedCount);
        Assert.Equal(originalEventIds.Order().ToArray(), result.RemovedEventIds.Order().ToArray());
        Assert.Equal(originalEventIds.Order().ToArray(), handler.RemovedEventIds.Order().ToArray());

        var regeneratedEvents = await _dbContext
            .Events.Where(x => x.EventSeriesId == eventSeries.Id)
            .OrderBy(x => x.SeriesStartAtUtc)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, regeneratedEvents.Count);
        Assert.Empty(regeneratedEvents.Select(x => x.Id).Intersect(originalEventIds));
        Assert.All(regeneratedEvents, x =>
        {
            Assert.False(x.IsException);
            Assert.Equal("Regenerated series", x.Title);
        });
    }

    [Fact]
    public async Task RecreateAsync_WhenHandlerCancelsRemovedEvent_PreservesCancelledEventAndCreatesReplacement()
    {
        // Arrange
        var eventSeries = await AddSeriesAsync("FREQ=DAILY;COUNT=1");
        var handler = new CountingMaterializationHandler();
        await _service.MaterializeAsync(eventSeries, CreateOptions(), handler, TestContext.Current.CancellationToken);
        var originalEvent = await _dbContext.Events.SingleAsync(TestContext.Current.CancellationToken);
        handler.CancelRemovedEventIds.Add(originalEvent.Id);
        eventSeries.Title = "Replacement";
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.RecreateAsync(
            eventSeries,
            CreateOptions(),
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(1, result.RemovedCount);
        var events = await _dbContext
            .Events.Where(x => x.EventSeriesId == eventSeries.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, events.Count);
        Assert.Equal(originalEvent.Id, events[0].Id);
        Assert.Equal(CalendarEventStatusTypeCodes.Cancelled, events[0].StatusTypeCode);
        Assert.NotEqual(originalEvent.Id, events[1].Id);
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, events[1].StatusTypeCode);
        Assert.Equal("Replacement", events[1].Title);
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

    private static EventSeriesMaterializationOptions CreateOptions(
        TimeSpan? maximumDuration = null,
        int maximumOccurrences = 400
    ) =>
        new()
        {
            ValidationOptions = new RecurrenceValidationOptions
            {
                MaximumDuration = maximumDuration ?? TimeSpan.FromDays(365),
                MaximumOccurrences = maximumOccurrences,
                RequireBoundedRule = true,
            },
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

    private static CalendarDateTimeService CreateCalendarDateTimeService() =>
        new(Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "America/Vancouver" }));

    private sealed class CountingMaterializationHandler : IEventSeriesMaterializationHandler
    {
        public string SourceModule => CalendarConstants.SourceModule;

        public string EventTypeCode => CalendarEventTypeCodes.General;

        public int ContextCreateCount { get; private set; }

        public Guid ContextId { get; } = Guid.NewGuid();

        public List<Guid> CreatedContextIds { get; } = [];

        public List<int> RemovedEventIds { get; } = [];

        public HashSet<int> CancelRemovedEventIds { get; } = [];

        public Task<IEventSeriesMaterializationContext> CreateContextAsync(
            EventSeries eventSeries,
            CancellationToken cancellationToken
        )
        {
            ContextCreateCount++;
            return Task.FromResult<IEventSeriesMaterializationContext>(new CountingContext(ContextId));
        }

        public Task<bool> CanRecreateSeriesEntriesAsync(
            EventSeries eventSeries,
            IEnumerable<Event> events,
            CancellationToken cancellationToken
        ) => Task.FromResult(true);

        public Task OnEventCreatedAsync(
            EventSeries eventSeries,
            Event eventEntity,
            SeriesEntry occurrence,
            IEventSeriesMaterializationContext context,
            CancellationToken cancellationToken
        )
        {
            CreatedContextIds.Add(((CountingContext)context).Id);
            return Task.CompletedTask;
        }

        public Task OnEventUpdatedAsync(
            EventSeries eventSeries,
            Event eventEntity,
            SeriesEntry occurrence,
            IEventSeriesMaterializationContext context,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;

        public Task OnEventRemovedAsync(
            EventSeries eventSeries,
            Event eventEntity,
            IEventSeriesMaterializationContext context,
            CancellationToken cancellationToken
        )
        {
            RemovedEventIds.Add(eventEntity.Id);
            if (CancelRemovedEventIds.Contains(eventEntity.Id))
                eventEntity.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;
            return Task.CompletedTask;
        }
    }

    private sealed record CountingContext(Guid Id) : IEventSeriesMaterializationContext;
}
