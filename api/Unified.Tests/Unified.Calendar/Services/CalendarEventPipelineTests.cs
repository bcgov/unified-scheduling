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

    [Fact]
    public void BuildEvent_WhenEntryRequestIsValid_MapsSharedFieldsAndCallsFactory()
    {
        var request = new CalendarEventEntryRequest
        {
            Title = "  Entry title  ",
            Description = "  Description  ",
            StartAtUtc = StartAtUtc,
            EndAtUtc = StartAtUtc.AddHours(1),
            TimeZoneId = "  America/Vancouver  ",
        };
        var factory = new FakeEntryFactory();
        var service = CreateEntryService(factory);

        var result = service.BuildEvent(request, "test-module", "test-type");

        Assert.Same(factory.Entity, result);
        Assert.Same(request, factory.CreatedRequest);
        Assert.Equal("Entry title", factory.CreatedEvent!.Title);
        Assert.Equal("Description", factory.CreatedEvent.Description);
        Assert.Equal("America/Vancouver", factory.CreatedEvent.TimeZoneId);
        Assert.Equal(StartAtUtc, factory.CreatedEvent.SeriesStartAtUtc);
        Assert.Equal(StartAtUtc.AddHours(1), factory.CreatedEvent.SeriesEndAtUtc);
        Assert.Equal("test-module", factory.CreatedEvent.SourceModule);
        Assert.Equal("test-type", factory.CreatedEvent.EventTypeCode);
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, factory.CreatedEvent.StatusTypeCode);
    }

    [Fact]
    public void UpdateEvent_WhenEntryIsDraft_MapsMutableFieldsAndPreservesIdentityFields()
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

        var result = service.UpdateEvent(request, existing, "test-module", "test-type");

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
    public void UpdateEvent_WhenEntryIsNotDraft_ThrowsAndDoesNotUpdateFactoryEntity()
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

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.UpdateEvent(
                new CalendarEventEntryRequest { Title = "Updated", StartAtUtc = StartAtUtc },
                factory.Entity,
                "test-module",
                "test-type"
            )
        );

        Assert.Contains("draft status", exception.Message);
        Assert.Null(factory.UpdatedRequest);
    }

    [Fact]
    public void BuildEvent_WhenSeriesRequestIsValid_MapsSeriesAndCallsFactory()
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

        var result = service.BuildEvent(request, "ignored-module", "test-type");

        Assert.Same(factory.Entity, result);
        Assert.Same(request, factory.CreatedRequest);
        Assert.Equal("Series title", factory.CreatedSeries!.Title);
        Assert.Equal("America/Vancouver", factory.CreatedSeries.TimeZoneId);
        Assert.Equal("FREQ=WEEKLY", factory.CreatedSeries.RecurrenceRule);
        Assert.Equal("test-type", factory.CreatedSeries.EventTypeCode);
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, factory.CreatedSeries.StatusTypeCode);
    }

    [Fact]
    public void UpdateEvent_WhenSeriesIsDraft_MapsMutableFieldsAndCallsFactory()
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

        var result = service.UpdateEvent(request, existing, "ignored-module", "test-type");

        Assert.Same(existing, result);
        Assert.Same(request, factory.UpdatedRequest);
        Assert.Same(existing, factory.UpdatedEntity);
        Assert.Equal("FREQ=DAILY", existingSeries.RecurrenceRule);
        Assert.Equal("Updated title", existingSeries.Title);
        Assert.Equal(StartAtUtc.AddDays(1), existingSeries.StartAtUtc);
        Assert.Equal(StartAtUtc.AddDays(1).AddHours(1), existingSeries.EndAtUtc);
    }

    [Fact]
    public void UpdateEvent_WhenSeriesIsNotDraft_ThrowsAndDoesNotCallFactory()
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

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.UpdateEvent(
                new CalendarEventSeriesRequest
                {
                    Title = "Updated",
                    StartAtUtc = StartAtUtc,
                    RecurrenceRule = "FREQ=DAILY",
                },
                existing,
                "ignored-module",
                "test-type"
            )
        );

        Assert.Contains("draft status", exception.Message);
        Assert.Null(factory.UpdatedRequest);
    }

    [Fact]
    public void IsDeletable_WhenEntryBelongsToSeries_Throws()
    {
        var factory = new FakeEntryFactory
        {
            Event = new Event { EventSeriesId = 3, StatusTypeCode = CalendarEventStatusTypeCodes.Draft },
        };
        var service = CreateEntryService(factory);

        var exception = Assert.Throws<InvalidOperationException>(() => service.IsDeletable(factory.Entity));

        Assert.Contains("deleted through their series", exception.Message);
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
