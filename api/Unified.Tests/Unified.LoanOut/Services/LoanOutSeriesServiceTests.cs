using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Calendar.Factories;
using Unified.Calendar.Mappings;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Time;
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
    private static readonly DateTimeOffset Start = new TimeZoneService().FromDateStringToStartOfDayInTimeZone(
        "2026-06-01",
        "America/Vancouver"
    );
    private static readonly DateTimeOffset End = new TimeZoneService().FromDateStringToEndOfDayInTimeZone(
        "2026-06-01",
        "America/Vancouver"
    );
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
        var timeZoneService = new TimeZoneService();
        var resolver = new CalendarTimeZoneResolver(
            Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "UTC" }),
            timeZoneService
        );
        var expander = new IcalNetRecurrenceExpander(timeZoneService, resolver);
        _service = new LoanOutSeriesService(
            NullLogger<LoanOutSeriesService>.Instance,
            _db,
            new LoanOutEventMapper(eventMapper),
            calendarSeriesService,
            new EventSeriesMaterializationService(
                _db,
                new IcalNetRecurrenceRuleValidator(expander, timeZoneService, resolver),
                expander
            ),
            new LoanOutSeriesMaterializationHandler(_db),
            _schedulingLoanOutService,
            expander
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
            .SingleAsync(TestContext.Current.CancellationToken);
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
            .SingleAsync(TestContext.Current.CancellationToken);
        series.EventSeries!.AllDay = false;
        foreach (var eventEntity in _db.Events)
            eventEntity.AllDay = false;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _service.UpdateLoanOutSeriesAsync(
            created.Id,
            CreateRequest() with
            {
                StartAtUtc = Start.AddDays(7),
                EndAtUtc = End.AddDays(7),
            },
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
                    StartAtUtc = Start.AddDays(7),
                    EndAtUtc = End.AddDays(7),
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
    [InlineData(30, 19, 22, true)]
    [InlineData(30, 19, 10, false)]
    [InlineData(19, 30, 22, false)]
    [InlineData(30, 30, 10, false)]
    public async Task UpdateLoanOutSeriesAsync_WhenOccurrenceDatesAreRetained_ChecksWithdrawnShifts(
        int originalEndDay,
        int newEndDay,
        int shiftDay,
        bool blocked
    )
    {
        var ct = TestContext.Current.CancellationToken;
        var request = CreateRequest() with
        {
            StartAtUtc = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 12, 2, 0, 0, 0, TimeSpan.Zero),
            TimeZoneId = "UTC",
            RecurrenceRule = $"FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR;UNTIL=202612{originalEndDay:00}T235959Z",
        };
        var created = await _service.CreateLoanOutSeriesAsync(request, ct);
        var shiftDate = new DateTimeOffset(2026, 12, shiftDay, 12, 0, 0, TimeSpan.Zero);
        _schedulingLoanOutService.ConflictingShiftAtUtc = shiftDate;
        var updatedRequest = request with
        {
            RecurrenceRule = $"FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR;UNTIL=202612{newEndDay:00}T235959Z",
        };

        if (blocked)
        {
            var exception = await Assert.ThrowsAsync<ConflictValidationException>(() =>
                _service.UpdateLoanOutSeriesAsync(created.Id, updatedRequest, ct)
            );
            Assert.True(exception.Errors.ContainsKey("LoanOut"));
            Assert.Empty(_schedulingLoanOutService.RestoredEntries);
        }
        else
        {
            var updated = await _service.UpdateLoanOutSeriesAsync(created.Id, updatedRequest, ct);
            Assert.NotNull(updated);
            Assert.Equal(updatedRequest.RecurrenceRule, updated.RecurrenceRule);
        }
        Assert.Equal(originalEndDay > newEndDay ? 10 : null, _schedulingLoanOutService.LastCheckedLocationId);
        _db.ChangeTracker.Clear();
        var retained = await _service.GetLoanOutSeriesByIdAsync(created.Id, ct);
        Assert.NotNull(retained);
        Assert.Equal(blocked ? request.RecurrenceRule : updatedRequest.RecurrenceRule, retained.RecurrenceRule);
        if (blocked)
            Assert.Equal(created.LoanOutEntryIds.Order(), retained.LoanOutEntryIds.Order());
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
            AllDay = true,
            RecurrenceRule = "FREQ=DAILY;COUNT=2",
            StartAtUtc = Start,
            EndAtUtc = End,
            TimeZoneId = "America/Vancouver",
            LocationId = 5,
            LoanOutLocationId = 10,
            UserIds = [UserA],
        };

    [Theory]
    [InlineData("FREQ=WEEKLY;BYDAY=MO,TH;COUNT=2", 6, 4, true)]
    [InlineData("FREQ=WEEKLY;BYDAY=MO,TH;COUNT=2", 6, 2, false)]
    [InlineData("FREQ=MONTHLY;BYMONTHDAY=1,15;COUNT=2", 6, 15, true)]
    [InlineData("FREQ=MONTHLY;BYMONTHDAY=1,15;COUNT=2", 6, 2, false)]
    [InlineData("FREQ=YEARLY;BYMONTH=6,12;BYMONTHDAY=1;COUNT=2", 12, 1, true)]
    [InlineData("FREQ=YEARLY;BYMONTH=6,12;BYMONTHDAY=1;COUNT=2", 6, 2, false)]
    public async Task CreateLoanOutSeriesAsync_WithSelectedRecurrenceDates_ChecksOnlyOccurrences(
        string recurrenceRule,
        int existingMonth,
        int existingDay,
        bool conflicts
    )
    {
        var ct = TestContext.Current.CancellationToken;
        var existingStart = new DateTimeOffset(2026, existingMonth, existingDay, 16, 0, 0, TimeSpan.Zero);
        await _service.CreateLoanOutSeriesAsync(
            CreateRequest() with
            {
                StartAtUtc = existingStart,
                EndAtUtc = existingStart.AddHours(4),
                RecurrenceRule = "FREQ=DAILY;COUNT=1",
            },
            ct
        );
        var request = CreateRequest() with { RecurrenceRule = recurrenceRule };
        if (conflicts)
        {
            await Assert.ThrowsAsync<ConflictValidationException>(() => _service.CreateLoanOutSeriesAsync(request, ct));
            Assert.False(_db.ChangeTracker.HasChanges());
            Assert.Equal(1, await _db.LoanOutSeries.CountAsync(ct));
        }
        else
        {
            var created = await _service.CreateLoanOutSeriesAsync(request, ct);
            Assert.Equal(2, await _db.LoanOutEntries.CountAsync(entry => entry.LoanOutSeriesId == created.Id, ct));
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SaveLoanOutSeriesAsync_WhenLaterOccurrenceOverlaps_RejectsWithoutSaving(
        bool update,
        bool regenerate
    )
    {
        var ct = TestContext.Current.CancellationToken;
        var original = update ? await _service.CreateLoanOutSeriesAsync(CreateRequest(), ct) : null;
        _db.LoanOutEntries.Add(
            new LoanOutEntry
            {
                LoanOutLocationId = 5,
                Event = new Event
                {
                    Title = "Existing standalone loan-out",
                    StartAtUtc = Start.AddDays(update ? 3 : 1).AddHours(5),
                    EndAtUtc = Start.AddDays(update ? 3 : 1).AddHours(6),
                    TimeZoneId = "America/Vancouver",
                    LocationId = 5,
                    SourceModule = LoanOutConstants.SourceModule,
                    EventTypeCode = LoanOutConstants.LoanOutEventTypeCode,
                    StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
                },
                Users = [new LoanOutEntryUser { UserId = UserA }],
            }
        );
        await _db.SaveChangesAsync(ct);
        var request = CreateRequest() with
        {
            StartAtUtc = update && regenerate ? Start.AddDays(2) : Start,
            EndAtUtc = update && regenerate ? End.AddDays(2) : End,
            RecurrenceRule = update && !regenerate ? "FREQ=DAILY;COUNT=4" : "FREQ=DAILY;COUNT=2",
        };
        await Assert.ThrowsAsync<ConflictValidationException>(async () =>
        {
            if (original is null)
                await _service.CreateLoanOutSeriesAsync(request, ct);
            else
                await _service.UpdateLoanOutSeriesAsync(original.Id, request, ct);
        });
        if (!update)
            Assert.False(_db.ChangeTracker.HasChanges());
        _db.ChangeTracker.Clear();
        Assert.Equal(update ? 1 : 0, await _db.LoanOutSeries.CountAsync(ct));
        Assert.Equal(update ? 3 : 1, await _db.LoanOutEntries.CountAsync(ct));
        if (original is not null)
        {
            var saved = (await _service.GetLoanOutSeriesByIdAsync(original.Id, ct))!;
            Assert.Equal(Start, saved.StartAtUtc);
            Assert.Equal("FREQ=DAILY;COUNT=2", saved.RecurrenceRule);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateLoanOutSeriesAsync_WhenAddingUser_ChecksNewAndMovedDates(bool moveOccurrence)
    {
        var ct = TestContext.Current.CancellationToken;
        var userB = Guid.NewGuid();
        _db.Users.Add(
            new User
            {
                Id = userB,
                IdirName = "user.b",
                IsEnabled = true,
                FirstName = "User",
                LastName = "B",
                Email = "b@example.com",
                Gender = Gender.Other,
            }
        );
        await _db.SaveChangesAsync(ct);
        var created = await _service.CreateLoanOutSeriesAsync(CreateRequest(), ct);
        if (moveOccurrence)
        {
            var moved = await _db
                .LoanOutEntries.Include(entry => entry.Event)
                .Where(entry => entry.LoanOutSeriesId == created.Id)
                .OrderBy(entry => entry.Event!.StartAtUtc)
                .LastAsync(ct);
            moved.Event!.StartAtUtc = Start.AddDays(2);
            moved.Event.EndAtUtc = End.AddDays(2);
            moved.Event.IsException = true;
        }
        _db.LoanOutEntries.Add(
            new LoanOutEntry
            {
                LoanOutLocationId = 10,
                Event = new Event
                {
                    Title = "B standalone",
                    StartAtUtc = Start.AddDays(2),
                    EndAtUtc = End.AddDays(2),
                    TimeZoneId = "America/Vancouver",
                    LocationId = 5,
                    SourceModule = LoanOutConstants.SourceModule,
                    EventTypeCode = LoanOutConstants.LoanOutEventTypeCode,
                    StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
                },
                Users = [new LoanOutEntryUser { UserId = userB }],
            }
        );
        await _db.SaveChangesAsync(ct);

        await Assert.ThrowsAsync<ConflictValidationException>(() =>
            _service.UpdateLoanOutSeriesAsync(
                created.Id,
                CreateRequest() with
                {
                    UserIds = [UserA, userB],
                    RecurrenceRule = moveOccurrence ? "FREQ=DAILY;COUNT=2" : "FREQ=DAILY;COUNT=3",
                },
                ct
            )
        );

        _db.ChangeTracker.Clear();
        var saved = await _service.GetLoanOutSeriesByIdAsync(created.Id, ct);
        Assert.Equal([UserA], saved!.UserIds);
        Assert.Equal(3, await _db.LoanOutEntries.CountAsync(ct));
    }

    [Fact]
    public async Task UpdateLoanOutSeriesAsync_WhenOccurrencesShareDate_RejectsConflictBetweenOccurrences()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await _service.CreateLoanOutSeriesAsync(CreateRequest(), ct);
        var moved = await _db
            .LoanOutEntries.Include(entry => entry.Event)
            .OrderBy(entry => entry.Event!.StartAtUtc)
            .LastAsync(ct);
        moved.Event!.StartAtUtc = Start;
        moved.Event.EndAtUtc = End;
        moved.Event.IsException = true;
        await _db.SaveChangesAsync(ct);

        await Assert.ThrowsAsync<ConflictValidationException>(() =>
            _service.UpdateLoanOutSeriesAsync(created.Id, CreateRequest() with { Title = "Changed title" }, ct)
        );
        _db.ChangeTracker.Clear();
        Assert.Equal("Loan out series", (await _service.GetLoanOutSeriesByIdAsync(created.Id, ct))!.Title);
    }

    [Fact]
    public async Task UpdateLoanOutSeriesAsync_WhenOccurrenceUsersAreOverridden_DoesNotReportFalseConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var userB = Guid.NewGuid();
        _db.Users.Add(
            new User
            {
                Id = userB,
                IdirName = "override.b",
                IsEnabled = true,
                FirstName = "User",
                LastName = "B",
                Email = "override.b@example.com",
                Gender = Gender.Other,
            }
        );
        await _db.SaveChangesAsync(ct);
        var created = await _service.CreateLoanOutSeriesAsync(CreateRequest(), ct);
        var moved = await _db
            .LoanOutEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .OrderBy(entry => entry.Event!.StartAtUtc)
            .LastAsync(ct);
        moved.Event!.StartAtUtc = Start.AddDays(2);
        moved.Event.EndAtUtc = End.AddDays(2);
        moved.Event.IsException = true;
        _db.LoanOutEntryUsers.RemoveRange(moved.Users);
        moved.Users = [new LoanOutEntryUser { UserId = userB }];
        await _db.SaveChangesAsync(ct);
        await _service.CreateLoanOutSeriesAsync(
            CreateRequest() with
            {
                StartAtUtc = Start.AddDays(2),
                EndAtUtc = End.AddDays(2),
                RecurrenceRule = "FREQ=DAILY;COUNT=1",
            },
            ct
        );

        var updated = await _service.UpdateLoanOutSeriesAsync(
            created.Id,
            CreateRequest() with
            {
                Title = "Updated",
            },
            ct
        );

        Assert.Equal("Updated", updated!.Title);
        _db.ChangeTracker.Clear();
        var retained = await _db
            .LoanOutEntries.Include(entry => entry.Users)
            .SingleAsync(entry => entry.Id == moved.Id, ct);
        Assert.Equal(userB, Assert.Single(retained.Users).UserId);
    }

    [Theory]
    [InlineData(false, "2026-03-08", "2026-03-09T03:30:00Z", true)]
    [InlineData(true, "2026-03-08", "2026-03-09T03:30:00Z", true)]
    [InlineData(false, "2026-03-08", "2026-03-09T04:00:00Z", false)]
    [InlineData(true, "2026-03-08", "2026-03-09T04:00:00Z", false)]
    [InlineData(false, "2026-11-01", "2026-11-02T04:30:00Z", true)]
    [InlineData(true, "2026-11-01", "2026-11-02T04:30:00Z", true)]
    [InlineData(false, "2026-11-01", "2026-11-02T05:00:00Z", false)]
    [InlineData(true, "2026-11-01", "2026-11-02T05:00:00Z", false)]
    public async Task SaveLoanOutSeriesAsync_OnDaylightSavingTransition_UsesLocalDayBoundaries(
        bool update,
        string date,
        string existingStartUtc,
        bool conflicts
    )
    {
        var ct = TestContext.Current.CancellationToken;
        var original = update ? await _service.CreateLoanOutSeriesAsync(CreateRequest(), ct) : null;
        var existingStart = DateTimeOffset.Parse(existingStartUtc, System.Globalization.CultureInfo.InvariantCulture);
        await _service.CreateLoanOutSeriesAsync(
            CreateRequest() with
            {
                StartAtUtc = existingStart,
                EndAtUtc = existingStart.AddMinutes(15),
                TimeZoneId = "America/New_York",
                RecurrenceRule = "FREQ=DAILY;COUNT=1",
            },
            ct
        );
        var start = new TimeZoneService().FromDateStringToStartOfDayInTimeZone(date, "America/New_York");
        var request = CreateRequest() with
        {
            StartAtUtc = start,
            EndAtUtc = new TimeZoneService().FromDateStringToEndOfDayInTimeZone(date, "America/New_York"),
            TimeZoneId = "America/New_York",
            RecurrenceRule = "FREQ=DAILY;COUNT=1",
        };

        async Task SaveAsync()
        {
            if (original is null)
                await _service.CreateLoanOutSeriesAsync(request, ct);
            else
                await _service.UpdateLoanOutSeriesAsync(original.Id, request, ct);
        }

        if (conflicts)
            await Assert.ThrowsAsync<ConflictValidationException>(SaveAsync);
        else
            await SaveAsync();

        _db.ChangeTracker.Clear();
        Assert.Equal(conflicts && !update ? 1 : 2, await _db.LoanOutSeries.CountAsync(ct));
        Assert.Equal(conflicts ? (update ? 3 : 1) : 2, await _db.LoanOutEntries.CountAsync(ct));
        if (original is not null)
        {
            var saved = await _service.GetLoanOutSeriesByIdAsync(original.Id, ct);
            Assert.Equal(conflicts ? Start : start, saved!.StartAtUtc);
        }
    }

    private sealed class FakeSchedulingLoanOutService : ISchedulingLoanOutService
    {
        public Task InvalidateResolvedConflictOverridesAsync(
            IReadOnlyCollection<LoanOutEntry> entries,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public IReadOnlyCollection<LoanOutEntry> RestoredEntries { get; private set; } = [];

        public Task RestoreHomeLocationUserShiftsAsync(
            IReadOnlyCollection<LoanOutEntry> removedEntries,
            CancellationToken cancellationToken = default
        )
        {
            RestoredEntries = removedEntries;
            return Task.CompletedTask;
        }

        public bool HasShiftConflict { get; set; }
        public DateTimeOffset? ConflictingShiftAtUtc { get; set; }
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
            return Task.FromResult(
                HasShiftConflict
                    || (
                        ConflictingShiftAtUtc is { } shiftAt
                        && loanOutEntries.Any(entry =>
                            entry.Event!.StartAtUtc <= shiftAt && entry.Event.EndAtUtc > shiftAt
                        )
                    )
            );
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
