using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Calendar.Factories;
using Unified.Calendar.Mappings;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.UserManagement;
using Unified.LoanOut.Constants;
using Unified.LoanOut.Factories;
using Unified.LoanOut.Mappers;
using Unified.LoanOut.Models;
using Unified.LoanOut.Services;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.LoanOut.Services;

public sealed class LoanOutSeriesServiceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset EffectiveDate = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");
    private SqliteConnection _connection = null!;
    private UnifiedDbContext _db = null!;
    private LoanOutSeriesService _service = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.CreateFunction("now", () => DateTimeOffset.UtcNow.ToString("O"));
        await _connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(_connection).Options;
        _db = new SqliteTestUnifiedDbContext(options);
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        _db.EventTypes.Add(
            new EventType
            {
                Code = LoanOutConstants.LoanOutEventTypeCode,
                Description = LoanOutConstants.LoanOutEventTypeDescription,
                EffectiveDate = EffectiveDate,
            }
        );
        _db.EventStatusTypes.AddRange(
            new EventStatusType
            {
                Code = CalendarEventStatusTypeCodes.Draft,
                Description = CalendarEventStatusTypeCodes.Draft,
                EffectiveDate = EffectiveDate,
            },
            new EventStatusType
            {
                Code = CalendarEventStatusTypeCodes.Active,
                Description = CalendarEventStatusTypeCodes.Active,
                EffectiveDate = EffectiveDate,
            }
        );
        _db.Locations.Add(
            new Location
            {
                Id = 5,
                AgencyId = "A5",
                Name = "Location 5",
                Timezone = "America/Vancouver",
            }
        );
        _db.Locations.Add(
            new Location
            {
                Id = 10,
                AgencyId = "A10",
                Name = "Loan out location",
                Timezone = "America/Vancouver",
            }
        );
        _db.Users.Add(
            new User
            {
                Id = UserA,
                IdirName = "test.user",
                IsEnabled = true,
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                Gender = Gender.Other,
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var eventMapper = new CalendarEventMapper();
        var eventFactory = new LoanOutEventFactory();
        var calendarSeriesService = new CalendarEventSeriesService<LoanOutSeriesRequest, LoanOutSeries>(
            eventFactory,
            eventMapper,
            new CalendarLifecycleService()
        );
        _service = new LoanOutSeriesService(
            NullLogger<LoanOutSeriesService>.Instance,
            _db,
            new LoanOutEventMapper(eventMapper),
            calendarSeriesService,
            new FakeMaterializationService(),
            new LoanOutSeriesMaterializationHandler(_db)
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task CreateLoanOutSeriesAsync_PersistsSeriesAndMaterializedEntries()
    {
        var response = await _service.CreateLoanOutSeriesAsync(CreateRequest(), TestContext.Current.CancellationToken);

        var series = await _db
            .LoanOutSeries.Include(item => item.EventSeries)
            .Include(item => item.Users)
            .Include(item => item.LoanOutEntries)
            .SingleAsync();
        Assert.Equal(series.Id, response.Id);
        Assert.Equal(10, response.LoanOutLocationId);
        Assert.Equal(2, response.LoanOutEntryIds.Count);
        Assert.Equal(2, _db.Events.Count());
        Assert.Equal(LoanOutConstants.SourceModule, series.EventSeries!.Events.First().SourceModule);
        Assert.Equal(UserA, Assert.Single(series.Users).UserId);
        Assert.All(
            series.LoanOutEntries,
            entry =>
            {
                Assert.Equal(10, entry.LoanOutLocationId);
                Assert.Equal(UserA, Assert.Single(entry.Users).UserId);
            }
        );
    }

    [Fact]
    public async Task GetLoanOutSeriesByIdAsync_WhenMissing_ReturnsNull()
    {
        var response = await _service.GetLoanOutSeriesByIdAsync(123, TestContext.Current.CancellationToken);

        Assert.Null(response);
    }

    [Fact]
    public async Task UpdateLoanOutSeriesAsync_WhenMissing_ReturnsNull()
    {
        var response = await _service.UpdateLoanOutSeriesAsync(
            123,
            CreateRequest(),
            TestContext.Current.CancellationToken
        );

        Assert.Null(response);
    }

    [Fact]
    public async Task DeleteLoanOutSeriesAsync_WhenMissing_ReturnsFalse()
    {
        var deleted = await _service.DeleteLoanOutSeriesAsync(123, TestContext.Current.CancellationToken);

        Assert.False(deleted);
    }

    private static LoanOutSeriesRequest CreateRequest() =>
        new()
        {
            Title = "Loan out series",
            RecurrenceRule = "FREQ=DAILY;COUNT=2",
            StartAtUtc = Start,
            EndAtUtc = Start.AddHours(4),
            TimeZoneId = "America/Vancouver",
            LocationId = 5,
            LoanOutLocationId = 10,
            UserIds = [UserA],
        };

    private sealed class FakeMaterializationService : IEventSeriesMaterializationService
    {
        public async Task MaterializeAsync<TContext>(
            EventSeries eventSeries,
            RecurrenceValidationOptions options,
            IEventSeriesMaterializationHandler<TContext> handler,
            TContext context,
            CancellationToken cancellationToken
        )
            where TContext : notnull
        {
            for (var occurrence = 0; occurrence < 2; occurrence++)
            {
                var start = eventSeries.StartAtUtc.AddDays(occurrence);
                var eventEntity = new Event
                {
                    EventSeries = eventSeries,
                    Title = eventSeries.Title,
                    Description = eventSeries.Description,
                    Notes = eventSeries.Notes,
                    Color = eventSeries.Color,
                    StartAtUtc = start,
                    EndAtUtc = start.Add(eventSeries.EndAtUtc!.Value - eventSeries.StartAtUtc),
                    SeriesStartAtUtc = start,
                    SeriesEndAtUtc = start.Add(eventSeries.EndAtUtc.Value - eventSeries.StartAtUtc),
                    TimeZoneId = eventSeries.TimeZoneId,
                    LocationId = eventSeries.LocationId,
                    SourceModule = LoanOutConstants.SourceModule,
                    EventTypeCode = LoanOutConstants.LoanOutEventTypeCode,
                    StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
                };
                eventSeries.Events.Add(eventEntity);
                await handler.OnMaterializedEventCreatedAsync(
                    eventSeries,
                    eventEntity,
                    new SeriesEntry { StartAtUtc = start, EndAtUtc = eventEntity.EndAtUtc },
                    context,
                    cancellationToken
                );
            }
        }

        public Task RegenerateDraftSeriesAsync<TContext>(
            EventSeries eventSeries,
            RecurrenceValidationOptions options,
            IEventSeriesMaterializationHandler<TContext> handler,
            TContext context,
            CancellationToken cancellationToken
        )
            where TContext : notnull => throw new NotSupportedException();
    }
}
