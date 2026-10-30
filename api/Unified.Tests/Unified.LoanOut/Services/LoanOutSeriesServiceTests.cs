using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Calendar.Factories;
using Unified.Calendar.Mappings;
using Unified.Calendar.Services;
using Unified.Common.Validation;
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
using Unified.Scheduling.Services;
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
    private readonly FakeSchedulingLoanOutService _schedulingLoanOutService = new();

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
            new LoanOutSeriesMaterializationHandler(_db),
            _schedulingLoanOutService
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
        Assert.True(response.AllDay);
        Assert.True(series.EventSeries!.AllDay);
        Assert.All(_db.Events, eventEntity => Assert.True(eventEntity.AllDay));
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
    public async Task GetLoanOutSeriesByUserAsync_AfterReload_PreservesRecurrenceAndOccurrenceIdentifiers()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await _service.CreateLoanOutSeriesAsync(CreateRequest(), ct);
        _db.ChangeTracker.Clear();

        var response = Assert.Single(await _service.GetLoanOutSeriesByUserAsync(UserA, ct));

        Assert.Equal(created.Id, response.Id);
        Assert.Equal(created.EventSeriesId, response.EventSeriesId);
        Assert.Equal(created.StartAtUtc, response.StartAtUtc);
        Assert.Equal(created.EndAtUtc, response.EndAtUtc);
        Assert.Equal(created.RecurrenceRule, response.RecurrenceRule);
        Assert.Equal(created.LoanOutLocationId, response.LoanOutLocationId);
        Assert.Equal([UserA], response.UserIds);
        Assert.Equal(created.EventIds.Order(), response.EventIds.Order());
        Assert.Equal(created.LoanOutEntryIds.Order(), response.LoanOutEntryIds.Order());
        Assert.Empty(await _service.GetLoanOutSeriesByUserAsync(Guid.Empty, ct));
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
    public async Task UpdateLoanOutSeriesAsync_AlwaysPersistsSeriesAndEventsAsAllDay()
    {
        var created = await _service.CreateLoanOutSeriesAsync(CreateRequest(), TestContext.Current.CancellationToken);
        var series = await _db
            .LoanOutSeries.Include(item => item.EventSeries!)
                .ThenInclude(eventSeries => eventSeries.Events)
            .Include(item => item.LoanOutEntries)
                .ThenInclude(entry => entry.Event)
            .SingleAsync();
        series.EventSeries!.AllDay = false;
        foreach (var eventEntity in _db.Events)
            eventEntity.AllDay = false;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _service.UpdateLoanOutSeriesAsync(
            created.Id,
            CreateRequest(),
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(response);
        Assert.True(response.AllDay);
        Assert.True(series.EventSeries.AllDay);
        Assert.All(_db.Events, eventEntity => Assert.True(eventEntity.AllDay));
    }

    [Fact]
    public async Task DeleteLoanOutSeriesAsync_WhenMissing_ReturnsFalse()
    {
        var deleted = await _service.DeleteLoanOutSeriesAsync(123, TestContext.Current.CancellationToken);

        Assert.False(deleted);
    }

    [Fact]
    public async Task UpdateAndDeleteLoanOutSeriesAsync_WhenShiftExistsAtLoanOutLocation_ThrowsConflict()
    {
        var created = await _service.CreateLoanOutSeriesAsync(CreateRequest(), TestContext.Current.CancellationToken);
        _schedulingLoanOutService.HasShiftConflict = true;

        await Assert.ThrowsAsync<ConflictValidationException>(() =>
            _service.UpdateLoanOutSeriesAsync(
                created.Id,
                CreateRequest() with
                {
                    LoanOutLocationId = 5,
                    Title = "Blocked update",
                },
                TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(10, _schedulingLoanOutService.LastCheckedLocationId);
        _db.ChangeTracker.Clear();
        var retained = await _db
            .LoanOutSeries.Include(series => series.EventSeries)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(10, retained.LoanOutLocationId);
        Assert.Equal("Loan out series", retained.EventSeries!.Title);
        await Assert.ThrowsAsync<ConflictValidationException>(() =>
            _service.DeleteLoanOutSeriesAsync(created.Id, TestContext.Current.CancellationToken)
        );
    }

    [Theory]
    [InlineData(false, true, 999)]
    [InlineData(true, true, 999)]
    [InlineData(false, false, 999)]
    [InlineData(true, false, 999)]
    [InlineData(false, true, 0)]
    [InlineData(true, true, -1)]
    [InlineData(false, false, 0)]
    [InlineData(true, false, -1)]
    public async Task LocationGuard_InvalidLocation_RejectsWithoutSaving(bool update, bool source, int locationId)
    {
        var ct = TestContext.Current.CancellationToken;
        var original = update ? await _service.CreateLoanOutSeriesAsync(CreateRequest(), ct) : null;
        var request = source
            ? CreateRequest() with
            {
                LocationId = locationId,
                Title = "Rejected",
            }
            : CreateRequest() with
            {
                LoanOutLocationId = locationId,
                Title = "Rejected",
            };
        var beforeSeries = await _db.LoanOutSeries.CountAsync(ct);
        var beforeEntries = await _db.LoanOutEntries.CountAsync(ct);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            if (update)
                await _service.UpdateLoanOutSeriesAsync(original!.Id, request, ct);
            else
                await _service.CreateLoanOutSeriesAsync(request, ct);
        });
        Assert.Contains(
            source ? "home location" : "loan-out location",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
        _db.ChangeTracker.Clear();
        Assert.Equal(beforeSeries, await _db.LoanOutSeries.CountAsync(ct));
        Assert.Equal(beforeEntries, await _db.LoanOutEntries.CountAsync(ct));
        Assert.False(await _db.EventSeries.AnyAsync(item => item.Title == "Rejected", ct));
    }

    [Fact]
    public async Task LocationGuard_OptionalSource_AllowsMissingSource()
    {
        var result = await _service.CreateLoanOutSeriesAsync(
            CreateRequest() with
            {
                LocationId = null,
            },
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(false, "missing")]
    [InlineData(true, "missing")]
    [InlineData(false, "mixed")]
    [InlineData(true, "mixed")]
    [InlineData(false, "empty")]
    [InlineData(true, "empty")]
    [InlineData(false, "empty-id")]
    [InlineData(true, "empty-id")]
    [InlineData(false, "duplicate")]
    [InlineData(true, "duplicate")]
    public async Task UserGuard_InvalidUsers_RejectsWithoutSaving(bool update, string scenario)
    {
        var ct = TestContext.Current.CancellationToken;
        var original = update ? await _service.CreateLoanOutSeriesAsync(CreateRequest(), ct) : null;
        var missing = new Guid("99999999-9999-9999-9999-999999999999");
        Guid[] users = scenario switch
        {
            "empty" => [],
            "empty-id" => [Guid.Empty],
            "duplicate" => [UserA, UserA],
            "mixed" => [UserA, missing],
            _ => [missing],
        };
        var request = CreateRequest() with { UserIds = users, Title = "Rejected users" };
        var beforeSeries = await _db.LoanOutSeries.CountAsync(ct);
        var beforeEntries = await _db.LoanOutEntries.CountAsync(ct);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            if (update)
                await _service.UpdateLoanOutSeriesAsync(original!.Id, request, ct);
            else
                await _service.CreateLoanOutSeriesAsync(request, ct);
        });
        if (scenario is "missing" or "mixed")
            Assert.Equal("One or more selected users were not found.", exception.Message);
        _db.ChangeTracker.Clear();
        Assert.Equal(beforeSeries, await _db.LoanOutSeries.CountAsync(ct));
        Assert.Equal(beforeEntries, await _db.LoanOutEntries.CountAsync(ct));
        Assert.False(await _db.EventSeries.AnyAsync(item => item.Title == "Rejected users", ct));
        if (update)
        {
            var saved = await _db.LoanOutSeries.Include(series => series.Users).SingleAsync(ct);
            Assert.Equal(UserA, Assert.Single(saved.Users).UserId);
        }
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
                    AllDay = eventSeries.AllDay,
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

    private sealed class FakeSchedulingLoanOutService : ISchedulingLoanOutService
    {
        public Task RestoreHomeLocationUserShiftsAsync(
            IReadOnlyCollection<LoanOutEntry> removedEntries,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public bool HasShiftConflict { get; set; }
        public int? LastCheckedLocationId { get; private set; }

        public Task<IReadOnlyList<IReadOnlyCollection<Guid>>> GetAssignableUserIdsByOccurrenceAsync(
            IReadOnlyCollection<Guid> userIds,
            IReadOnlyCollection<ShiftAssignmentLoanOutContext> assignmentOccurrences,
            int? locationId,
            string? timeZoneId,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<IReadOnlyCollection<Guid>>>([]);

        public Task<bool> HasShiftAtLoanOutLocationAsync(
            IEnumerable<LoanOutEntry> loanOutEntries,
            int loanOutLocationId,
            CancellationToken cancellationToken = default
        )
        {
            LastCheckedLocationId = loanOutLocationId;
            return Task.FromResult(HasShiftConflict);
        }

        public Task<bool> TransferLoanedOutUserShift(
            LoanOutEntry entry,
            bool deleteAssignments = false,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(false);

        public Task<bool> TransferLoanedOutUserShift(
            LoanOutSeries series,
            bool deleteAssignments = false,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(false);
    }
}
