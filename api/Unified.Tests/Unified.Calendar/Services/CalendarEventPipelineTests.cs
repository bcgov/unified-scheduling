using Microsoft.Extensions.Logging.Abstractions;
using Unified.Calendar.Factories;
using Unified.Calendar.Mappings;
using Unified.Calendar.Services;
using Unified.Common.Calendar.Scheduling.Models;
using Unified.Db.Models.Calendar;

namespace Unified.Tests.Calendar.Services;

public sealed class CalendarEventPipelineTests
{
    private static readonly DateTimeOffset StartAtUtc = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SeriesStartAtUtc = StartAtUtc.AddDays(-7);
    private static readonly DateTimeOffset SeriesEndAtUtc = SeriesStartAtUtc.AddHours(1);

    [Fact]
    public async Task BuildEventAsync_RunsValidatorsThenGuardOnceBeforeBuildingSeries()
    {
        var factory = new FakeSeriesFactory();
        ICalendarEventService<CalendarEventSeriesRequest, SeriesEntity, EventSeries> service = CreateSeriesService(
            factory
        );
        var request = new CalendarEventSeriesRequest { Title = "Series", StartAtUtc = StartAtUtc };
        var calls = new List<string>();
        using var cancellation = new CancellationTokenSource();
        var result = await service.BuildEventAsync(
            request,
            "test-module",
            "test-type",
            validators:
            [
                (input, ct) =>
                {
                    Assert.Same(request, input);
                    Assert.Equal(cancellation.Token, ct);
                    Assert.Null(factory.CreatedSeries);
                    calls.Add("validate");
                    return Task.CompletedTask;
                },
            ],
            guards: input =>
            {
                Assert.Same(request, input);
                Assert.Null(factory.CreatedSeries);
                calls.Add("guard");
            },
            cancellationToken: cancellation.Token
        );
        Assert.Equal(new[] { "validate", "guard" }, calls);
        Assert.Same(factory.Entity, result);
        Assert.Equal("test-type", factory.CreatedSeries!.EventTypeCode);
    }

    [Fact]
    public async Task UpdateEvent_WhenAdditionalGuardFails_StopsBeforeMutationAndRemainingGuards()
    {
        var factory = new FakeEntryFactory
        {
            Event = new Event
            {
                Title = "Original",
                EventTypeCode = "test-type",
                SourceModule = "test-module",
                StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
            },
        };
        ICalendarEventService<CalendarEventEntryRequest, EntryEntity, Event> service = CreateEntryService(factory);
        var request = new CalendarEventEntryRequest { Title = "Updated", StartAtUtc = StartAtUtc };
        var calls = new List<int>();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateEventAsync(
                request,
                factory.Entity,
                "test-module",
                "test-type",
                additionalGuards: _ =>
                {
                    Assert.Equal("Original", factory.Event.Title);
                    calls.Add(1);
                    throw new InvalidOperationException("Domain guard failed.");
                },
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(new[] { 1 }, calls);
        Assert.Equal("Original", factory.Event.Title);
        Assert.Null(factory.UpdatedRequest);
    }

    [Fact]
    public async Task UpdateEvent_WhenDefaultGuardFails_DoesNotRunAdditionalGuards()
    {
        var factory = new FakeEntryFactory
        {
            Event = new Event
            {
                EventTypeCode = "test-type",
                SourceModule = "test-module",
                StatusTypeCode = CalendarEventStatusTypeCodes.Active,
            },
        };
        var called = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateEntryService(factory)
                .UpdateEventAsync(
                    new CalendarEventEntryRequest(),
                    factory.Entity,
                    "test-module",
                    "test-type",
                    additionalGuards: _ => called = true,
                    cancellationToken: TestContext.Current.CancellationToken
                )
        );
        Assert.False(called);
        Assert.Null(factory.UpdatedRequest);
    }

    [Fact]
    public async Task BuildEvent_WhenEntryRequestIsValid_MapsSharedFieldsAndCallsFactory()
    {
        var request = new CalendarEventEntryRequest
        {
            Title = "  Entry title  ",
            Description = "  Description  ",
            StartAtUtc = StartAtUtc,
            EndAtUtc = StartAtUtc.AddHours(1),
            SeriesStartAtUtc = SeriesStartAtUtc,
            SeriesEndAtUtc = SeriesEndAtUtc,
            TimeZoneId = "  America/Vancouver  ",
        };
        var factory = new FakeEntryFactory();
        var service = CreateEntryService(factory);

        var result = await service.BuildEventAsync(
            request,
            "test-module",
            "test-type",
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Same(factory.Entity, result);
        Assert.Same(request, factory.CreatedRequest);
        Assert.Equal("Entry title", factory.CreatedEvent!.Title);
        Assert.Equal("Description", factory.CreatedEvent.Description);
        Assert.Equal("America/Vancouver", factory.CreatedEvent.TimeZoneId);
        Assert.Equal(SeriesStartAtUtc, factory.CreatedEvent.SeriesStartAtUtc);
        Assert.Equal(SeriesEndAtUtc, factory.CreatedEvent.SeriesEndAtUtc);
        Assert.Equal("test-module", factory.CreatedEvent.SourceModule);
        Assert.Equal("test-type", factory.CreatedEvent.EventTypeCode);
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, factory.CreatedEvent.StatusTypeCode);
    }

    [Fact]
    public async Task UpdateEvent_WhenEntryIsDraft_MapsMutableFieldsAndPreservesIdentityFields()
    {
        var existing = new EntryEntity();
        var existingEvent = new Event
        {
            Id = 42,
            EventSeriesId = 7,
            Title = "Old title",
            StartAtUtc = StartAtUtc,
            SeriesStartAtUtc = StartAtUtc,
            EventTypeCode = "test-type",
            SourceModule = "test-module",
            StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
        };
        var factory = new FakeEntryFactory { Event = existingEvent, UpdatedResult = existing };
        var service = CreateEntryService(factory);
        var request = new CalendarEventEntryRequest
        {
            Title = "  Updated title  ",
            StartAtUtc = StartAtUtc.AddHours(1),
        };

        var result = await service.UpdateEventAsync(
            request,
            existing,
            "test-module",
            "test-type",
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Same(existing, result);
        Assert.Same(existing, factory.UpdatedEntity);
        Assert.Same(request, factory.UpdatedRequest);
        Assert.Equal("Updated title", existingEvent.Title);
        Assert.Equal(StartAtUtc.AddHours(1), existingEvent.StartAtUtc);
        Assert.Equal(42, existingEvent.Id);
        Assert.Equal(7, existingEvent.EventSeriesId);
        Assert.Equal(StartAtUtc, existingEvent.SeriesStartAtUtc);
        Assert.Equal("test-type", existingEvent.EventTypeCode);
        Assert.Equal("test-module", existingEvent.SourceModule);
    }

    [Fact]
    public async Task UpdateEvent_WhenEntryIsNotDraft_ThrowsAndDoesNotUpdateFactoryEntity()
    {
        var factory = new FakeEntryFactory
        {
            Event = new Event
            {
                EventTypeCode = "test-type",
                SourceModule = "test-module",
                StatusTypeCode = CalendarEventStatusTypeCodes.Active,
            },
        };
        var service = CreateEntryService(factory);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateEventAsync(
                new CalendarEventEntryRequest { Title = "Updated", StartAtUtc = StartAtUtc },
                factory.Entity,
                "test-module",
                "test-type",
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("draft status", exception.Message);
        Assert.Null(factory.UpdatedRequest);
    }

    [Fact]
    public async Task BuildEvent_WhenSeriesRequestIsValid_MapsSeriesAndCallsFactory()
    {
        var request = new CalendarEventSeriesRequest
        {
            Title = "  Series title  ",
            StartAtUtc = StartAtUtc,
            RecurrenceRule = "FREQ=WEEKLY",
            TimeZoneId = "  America/Vancouver  ",
        };
        var factory = new FakeSeriesFactory();
        var service = CreateSeriesService(factory);

        var result = await service.BuildEventAsync(
            request,
            "ignored-module",
            "test-type",
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Same(factory.Entity, result);
        Assert.Same(request, factory.CreatedRequest);
        Assert.Equal("Series title", factory.CreatedSeries!.Title);
        Assert.Equal("America/Vancouver", factory.CreatedSeries.TimeZoneId);
        Assert.Equal("FREQ=WEEKLY", factory.CreatedSeries.RecurrenceRule);
        Assert.Equal("test-type", factory.CreatedSeries.EventTypeCode);
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, factory.CreatedSeries.StatusTypeCode);
    }

    [Fact]
    public async Task UpdateEvent_WhenSeriesIsDraft_MapsMutableFieldsAndCallsFactory()
    {
        var existingSeries = new EventSeries
        {
            Id = 9,
            Title = "Old title",
            StartAtUtc = StartAtUtc,
            EndAtUtc = StartAtUtc.AddHours(1),
            TimeZoneId = "America/Vancouver",
            RecurrenceRule = "FREQ=WEEKLY",
            EventTypeCode = "test-type",
            StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
        };
        var existing = new SeriesEntity();
        var factory = new FakeSeriesFactory { Series = existingSeries, UpdatedResult = existing };
        var service = CreateSeriesService(factory);
        var request = new CalendarEventSeriesRequest
        {
            Title = "Updated title",
            StartAtUtc = StartAtUtc.AddDays(1),
            EndAtUtc = StartAtUtc.AddDays(1).AddHours(1),
            TimeZoneId = "America/Vancouver",
            RecurrenceRule = "FREQ=DAILY",
        };

        var result = await service.UpdateEventAsync(
            request,
            existing,
            "ignored-module",
            "test-type",
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Same(existing, result);
        Assert.Same(request, factory.UpdatedRequest);
        Assert.Same(existing, factory.UpdatedEntity);
        Assert.Equal("FREQ=DAILY", existingSeries.RecurrenceRule);
        Assert.Equal("Updated title", existingSeries.Title);
        Assert.Equal(StartAtUtc.AddDays(1), existingSeries.StartAtUtc);
        Assert.Equal(StartAtUtc.AddDays(1).AddHours(1), existingSeries.EndAtUtc);
    }

    [Fact]
    public async Task UpdateEvent_WhenSeriesIsNotDraft_ThrowsAndDoesNotCallFactory()
    {
        var existing = new SeriesEntity();
        var factory = new FakeSeriesFactory
        {
            Series = new EventSeries
            {
                StartAtUtc = StartAtUtc,
                RecurrenceRule = "FREQ=WEEKLY",
                TimeZoneId = "America/Vancouver",
                StatusTypeCode = CalendarEventStatusTypeCodes.Active,
                EventTypeCode = "test-type",
            },
            UpdatedResult = existing,
        };
        var service = CreateSeriesService(factory);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateEventAsync(
                new CalendarEventSeriesRequest
                {
                    Title = "Updated",
                    StartAtUtc = StartAtUtc,
                    RecurrenceRule = "FREQ=DAILY",
                },
                existing,
                "ignored-module",
                "test-type",
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("draft status", exception.Message);
        Assert.Null(factory.UpdatedRequest);
    }

    [Fact]
    public void IsDeletable_WhenDraftEntryBelongsToSeries_ReturnsTrue()
    {
        var factory = new FakeEntryFactory
        {
            Event = new Event
            {
                EventSeriesId = 3,
                StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
                SourceModule = "test-module",
                EventTypeCode = "test-type",
            },
        };

        Assert.True(CreateEntryService(factory).IsDeletable(factory.Entity, "test-module", "test-type"));
    }

    private static CalendarEventEntryService<CalendarEventEntryRequest, EntryEntity> CreateEntryService(
        FakeEntryFactory factory
    ) =>
        new(
            NullLogger<CalendarEventEntryService<CalendarEventEntryRequest, EntryEntity>>.Instance,
            factory,
            new CalendarLifecycleService(),
            new CalendarEventMapper()
        );

    private static CalendarEventSeriesService<CalendarEventSeriesRequest, SeriesEntity> CreateSeriesService(
        FakeSeriesFactory factory
    ) => new(factory, new CalendarEventMapper(), new CalendarLifecycleService());

    private sealed class EntryEntity;

    private sealed class SeriesEntity;

    private sealed class FakeEntryFactory : ICalendarEventEntryFactory<CalendarEventEntryRequest, EntryEntity>
    {
        public EntryEntity Entity { get; } = new();
        public Event? Event { get; init; }
        public Event? CreatedEvent { get; private set; }
        public CalendarEventEntryRequest? CreatedRequest { get; private set; }
        public CalendarEventEntryRequest? UpdatedRequest { get; private set; }
        public EntryEntity? UpdatedEntity { get; private set; }
        public EntryEntity? UpdatedResult { get; init; }

        public EntryEntity CreateEntry(CalendarEventEntryRequest request, Event eventEntity)
        {
            CreatedRequest = request;
            CreatedEvent = eventEntity;
            return Entity;
        }

        public Event GetEvent(EntryEntity entity) => Event!;

        public EntryEntity UpdateEntry(CalendarEventEntryRequest request, EntryEntity existing)
        {
            UpdatedRequest = request;
            UpdatedEntity = existing;
            return UpdatedResult ?? existing;
        }
    }

    private sealed class FakeSeriesFactory : ICalendarEventSeriesFactory<CalendarEventSeriesRequest, SeriesEntity>
    {
        public SeriesEntity Entity { get; } = new();
        public EventSeries? Series { get; init; }
        public EventSeries? CreatedSeries { get; private set; }
        public CalendarEventSeriesRequest? CreatedRequest { get; private set; }
        public CalendarEventSeriesRequest? UpdatedRequest { get; private set; }
        public SeriesEntity? UpdatedEntity { get; private set; }
        public SeriesEntity? UpdatedResult { get; init; }

        public SeriesEntity CreateSeries(CalendarEventSeriesRequest request, EventSeries eventSeries)
        {
            CreatedRequest = request;
            CreatedSeries = eventSeries;
            return Entity;
        }

        public EventSeries GetEventSeries(SeriesEntity entity) => Series!;

        public SeriesEntity UpdateSeries(CalendarEventSeriesRequest request, SeriesEntity existing)
        {
            UpdatedRequest = request;
            UpdatedEntity = existing;
            return UpdatedResult ?? existing;
        }
    }
}
