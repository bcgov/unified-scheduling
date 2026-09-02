using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.Scheduling;
using Unified.Db.Models.Stats;
using Unified.Db.Models.UserManagement;
using Unified.Scheduling;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.Scheduling.Services;

public sealed class AssignmentSchedulingIntegrationTests : IAsyncLifetime
{
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserB = new("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 15, 19, 30, 0, TimeSpan.Zero);

    private SqliteConnection _connection = null!;
    private UnifiedDbContext _db = null!;
    private AssignmentDefinitionService _definitionService = null!;
    private AssignmentService _assignmentService = null!;
    private ShiftService _shiftService = null!;
    private ShiftAssignmentService _linkService = null!;
    private SchedulingCalendarService _calendarService = null!;
    private readonly TransactionIsolationRecorder _transactionIsolationRecorder = new();

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.CreateFunction("now", () => FixedNow.ToString("O"));
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_transactionIsolationRecorder)
            .Options;
        _db = new SqliteTestUnifiedDbContext(options);
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await SeedBaseDataAsync();

        var timeZoneService = new TimeZoneService();
        var timeZoneResolver = new CalendarTimeZoneResolver(
            Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "UTC" }),
            timeZoneService
        );
        var recurrenceExpander = new IcalNetRecurrenceExpander(timeZoneService, timeZoneResolver);
        var recurrenceRuleValidator = new IcalNetRecurrenceRuleValidator(
            recurrenceExpander,
            timeZoneService,
            timeZoneResolver
        );
        var materializationService = new EventSeriesMaterializationService(
            _db,
            recurrenceRuleValidator,
            recurrenceExpander
        );
        var timeProvider = new FixedTimeProvider(FixedNow);

        _definitionService = new AssignmentDefinitionService(
            NullLogger<AssignmentDefinitionService>.Instance,
            _db,
            timeProvider
        );
        _assignmentService = new AssignmentService(
            NullLogger<AssignmentService>.Instance,
            _db,
            materializationService,
            new AssignmentSeriesMaterializationHandler(_db),
            new CalendarLifecycleService(),
            timeZoneService,
            timeProvider
        );
        _shiftService = new ShiftService(
            NullLogger<ShiftService>.Instance,
            _db,
            materializationService,
            new ShiftSeriesMaterializationHandler(_db),
            timeZoneResolver,
            timeZoneService,
            new CalendarLifecycleService(),
            timeProvider
        );
        _linkService = new ShiftAssignmentService(NullLogger<ShiftAssignmentService>.Instance, _db);
        _calendarService = new SchedulingCalendarService(
            NullLogger<SchedulingCalendarService>.Instance,
            _db,
            timeZoneResolver,
            timeZoneService
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task UpdateAssignmentDefinitionAsync_WhenAssignmentsExist_DoesNotMutateSnapshots()
    {
        var request = CreateAssignmentSeriesRequest() with
        {
            CategoryId = 20,
            SubCategoryId = 21,
            Capacity = 7,
            Color = "#123456",
            StartAtUtc = At(8),
            EndAtUtc = At(16),
            TimeZoneId = "America/Vancouver",
        };
        var created = await _assignmentService.CreateAssignmentSeriesAsync(
            request,
            TestContext.Current.CancellationToken
        );

        await _definitionService.UpdateAssignmentDefinitionAsync(
            100,
            CreateDefinitionRequest() with
            {
                LocationId = 9,
                CategoryId = 10,
                SubCategoryId = 11,
                Color = "#abcdef",
                DefaultCapacity = 99,
                DefaultStartTime = "01:00",
                DefaultEndTime = "02:00",
            },
            TestContext.Current.CancellationToken
        );

        var result = await _assignmentService.GetAssignmentSeriesByIdAsync(
            created.Id,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(result);
        Assert.Equal(20, result.CategoryId);
        Assert.Equal(21, result.SubCategoryId);
        Assert.Equal(7, result.Capacity);
        Assert.Equal("#123456", result.Color);
        Assert.Equal(At(8), result.StartAtUtc);
        Assert.Equal(At(16), result.EndAtUtc);
        Assert.Equal("America/Vancouver", result.TimeZoneId);
        Assert.All(
            result.Entries,
            entry =>
            {
                Assert.Equal(20, entry.CategoryId);
                Assert.Equal(21, entry.SubCategoryId);
                Assert.Equal(7, entry.Capacity);
                Assert.Equal("#123456", entry.Color);
            }
        );
    }

    [Fact]
    public async Task CreateAssignmentSeriesAsync_WhenRequestOverridesDefinition_UsesRequestValues()
    {
        var request = CreateAssignmentSeriesRequest() with
        {
            CategoryId = 20,
            SubCategoryId = 21,
            Capacity = 12,
            Color = "frontend-color",
            StartAtUtc = At(7),
            EndAtUtc = At(15),
            TimeZoneId = "America/Toronto",
        };

        var result = await _assignmentService.CreateAssignmentSeriesAsync(
            request,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(20, result.CategoryId);
        Assert.Equal(21, result.SubCategoryId);
        Assert.Equal(12, result.Capacity);
        Assert.Equal("frontend-color", result.Color);
        Assert.Equal(At(7), result.StartAtUtc);
        Assert.Equal(At(15), result.EndAtUtc);
        Assert.Equal("America/Toronto", result.TimeZoneId);
    }

    [Fact]
    public async Task AssignmentEntryExceptionState_CreateUpdateAndDetach_TracksSeriesSlot()
    {
        var series = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );
        var seriesSlotStart = AtDay(2, 10);
        var seriesSlotEnd = AtDay(2, 12);
        var created = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(AtDay(2, 11), AtDay(2, 13)) with
            {
                AssignmentSeriesId = series.Id,
                SeriesStartAtUtc = seriesSlotStart,
                SeriesEndAtUtc = seriesSlotEnd,
            },
            TestContext.Current.CancellationToken
        );
        Assert.True(created.IsException);

        var aligned = await _assignmentService.UpdateAssignmentEntryAsync(
            created.Id,
            CreateAssignmentEntryUpdateRequest(seriesSlotStart, seriesSlotEnd) with
            {
                AssignmentSeriesId = series.Id,
            },
            TestContext.Current.CancellationToken
        );
        Assert.False(aligned!.IsException);

        var moved = await _assignmentService.UpdateAssignmentEntryAsync(
            created.Id,
            CreateAssignmentEntryUpdateRequest(AtDay(2, 11), AtDay(2, 13)) with
            {
                AssignmentSeriesId = series.Id,
            },
            TestContext.Current.CancellationToken
        );
        Assert.True(moved!.IsException);

        var detached = await _assignmentService.UpdateAssignmentEntryAsync(
            created.Id,
            CreateAssignmentEntryUpdateRequest(AtDay(2, 11), AtDay(2, 13)),
            TestContext.Current.CancellationToken
        );
        Assert.False(detached!.IsException);
    }

    [Fact]
    public async Task DefinitionCreate_NormalizesUtcBusinessDatesToMidnight()
    {
        var result = await _definitionService.CreateAssignmentDefinitionAsync(
            CreateDefinitionRequest() with
            {
                Name = "Normalized dates",
                EffectiveDateUtc = DateTimeOffset.Parse("2026-08-21T15:30:00Z"),
                ExpiryDateUtc = DateTimeOffset.Parse("2026-08-23T22:15:00Z"),
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(DateTimeOffset.Parse("2026-08-21T00:00:00Z"), result.EffectiveDateUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-08-23T00:00:00Z"), result.ExpiryDateUtc);
    }

    [Fact]
    public async Task CreateAssignmentSeriesAsync_WhenSubcategoryBelongsToAnotherCategory_Throws()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assignmentService.CreateAssignmentSeriesAsync(
                CreateAssignmentSeriesRequest() with
                {
                    CategoryId = 10,
                    SubCategoryId = 21,
                },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("does not belong to the category", exception.Message);
    }

    [Fact]
    public async Task CreateAssignmentEntryAsync_WhenDefinitionLocationDiffers_Throws()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assignmentService.CreateAssignmentEntryAsync(
                CreateAssignmentEntryRequest() with
                {
                    LocationId = 9,
                },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("must match the assignment definition location", exception.Message);
    }

    [Fact]
    public async Task UpdateAssignmentSeriesAsync_WhenRecurrenceChangesAndLinkExists_Throws()
    {
        var (shiftSeries, assignmentSeries) = await CreateLinkedSeriesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assignmentService.UpdateAssignmentSeriesAsync(
                assignmentSeries.Id,
                CreateAssignmentSeriesRequest() with
                {
                    RecurrenceRule = "FREQ=DAILY;COUNT=2",
                },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("cannot be changed after shift links exist", exception.Message);
        Assert.True(await _db.ShiftSeries.AnyAsync(x => x.Id == shiftSeries.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateShiftSeriesAsync_WhenRecurrenceChangesAndLinkExists_Throws()
    {
        var (shiftSeries, _) = await CreateLinkedSeriesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _shiftService.UpdateShiftSeriesAsync(
                shiftSeries.Id,
                CreateShiftSeriesRequest() with
                {
                    RecurrenceRule = "FREQ=DAILY;COUNT=2",
                },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("cannot be changed after assignment links exist", exception.Message);
    }

    [Fact]
    public async Task DeleteShiftSeriesAsync_WhenRetainedLinkIsSuppressed_RemovesSuppressionRow()
    {
        var (shiftSeries, _) = await CreateLinkedSeriesAsync();
        var link = await _db.ShiftAssignmentEntries.SingleAsync(TestContext.Current.CancellationToken);
        await _shiftService.PublishShiftEntryAsync(link.ShiftEntryId, TestContext.Current.CancellationToken);
        await _linkService.DeleteShiftEntryLinkAsync(link.Id, TestContext.Current.CancellationToken);

        Assert.True(await _shiftService.DeleteShiftSeriesAsync(shiftSeries.Id, TestContext.Current.CancellationToken));

        Assert.False(
            await _db.ShiftAssignmentEntries.AnyAsync(
                candidate => candidate.Id == link.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task DeleteAssignmentSeriesAsync_WhenRetainedLinkIsSuppressed_RemovesSuppressionRow()
    {
        var (_, assignmentSeries) = await CreateLinkedSeriesAsync();
        var link = await _db.ShiftAssignmentEntries.SingleAsync(TestContext.Current.CancellationToken);
        await _assignmentService.PublishAssignmentEntryAsync(
            link.AssignmentEntryId,
            TestContext.Current.CancellationToken
        );
        await _linkService.DeleteShiftEntryLinkAsync(link.Id, TestContext.Current.CancellationToken);

        Assert.True(
            await _assignmentService.DeleteAssignmentSeriesAsync(
                assignmentSeries.Id,
                TestContext.Current.CancellationToken
            )
        );

        Assert.False(
            await _db.ShiftAssignmentEntries.AnyAsync(
                candidate => candidate.Id == link.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Theory]
    [InlineData(10, 12, 12, 14, false)]
    [InlineData(10, 12, 8, 10, false)]
    [InlineData(10, 12, 11, 13, true)]
    [InlineData(10, 12, 10, 12, true)]
    public async Task LinkShiftEntryAsync_UsesUtcHalfOpenOverlap(
        int shiftStartHour,
        int shiftEndHour,
        int assignmentStartHour,
        int assignmentEndHour,
        bool shouldLink
    )
    {
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(shiftStartHour), At(shiftEndHour)),
            TestContext.Current.CancellationToken
        );
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(assignmentStartHour), At(assignmentEndHour)),
            TestContext.Current.CancellationToken
        );
        var operation = () =>
            _linkService.LinkShiftEntryAsync(
                new ShiftAssignmentEntryRequest
                {
                    ShiftEntryId = shift.Id,
                    AssignmentEntryId = assignment.Id,
                    UserIds = [UserA],
                },
                TestContext.Current.CancellationToken
            );

        if (shouldLink)
            Assert.NotNull(await operation());
        else
            await Assert.ThrowsAsync<InvalidOperationException>(operation);
    }

    [Fact]
    public async Task SchedulingCalendarAsync_WhenAssignmentUserFilterProvided_UsesActiveLinkUsers()
    {
        var (shift, assignment, _) = await CreateLinkedEntriesAsync();

        var matching = await GetCalendarAsync([UserA], includeShifts: false, includeAssignments: true);
        var nonMatching = await GetCalendarAsync([UserB], includeShifts: false, includeAssignments: true);

        var assignmentEvent = Assert.Single(matching.Events);
        Assert.Equal($"scheduling.assignment-entry.{assignment.Id}", assignmentEvent.Id);
        Assert.Equal([UserA.ToString()], assignmentEvent.ResourceIds);
        Assert.Empty(nonMatching.Events);
        Assert.True(await _db.ShiftEntries.AnyAsync(x => x.Id == shift.Id, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(true, true, 2)]
    public async Task SchedulingCalendarAsync_IncludeFlagsReturnOnlyPermittedEventTypes(
        bool includeShifts,
        bool includeAssignments,
        int expectedCount
    )
    {
        await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(17)),
            TestContext.Current.CancellationToken
        );
        await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(10), At(12)),
            TestContext.Current.CancellationToken
        );

        var result = await GetCalendarAsync(null, includeShifts, includeAssignments);

        Assert.Equal(expectedCount, result.Events.Count);
        Assert.Equal(
            includeShifts,
            result.Events.Any(item => item.Id.StartsWith("scheduling.shift-entry.", StringComparison.Ordinal))
        );
        Assert.Equal(
            includeAssignments,
            result.Events.Any(item => item.Id.StartsWith("scheduling.assignment-entry.", StringComparison.Ordinal))
        );
    }

    [Fact]
    public async Task SchedulingCalendarAsync_UsesHalfOpenRangeBoundaries()
    {
        await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(new DateTimeOffset(2026, 5, 31, 23, 0, 0, TimeSpan.Zero), At(0)),
            TestContext.Current.CancellationToken
        );
        await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(
                new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 2, 1, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );
        var overlapping = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(
                new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 2, 1, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );

        var result = await GetCalendarAsync(null, includeShifts: true, includeAssignments: false);

        var item = Assert.Single(result.Events);
        Assert.Equal($"scheduling.shift-entry.{overlapping.Id}", item.Id);
    }

    [Fact]
    public async Task GetAssignmentSeriesAsync_WhenDraftSeriesOverlapsRange_ReturnsDraftSeries()
    {
        var created = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );

        var results = await _assignmentService.GetAssignmentSeriesAsync(
            new AssignmentSeriesQueryParams
            {
                StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
                StartAtUtc = At(9),
                EndAtUtc = At(13),
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(created.Id, Assert.Single(results).Id);
    }

    [Fact]
    public async Task ExpireAssignmentEntryAsync_UsesInjectedTimeProvider()
    {
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(),
            TestContext.Current.CancellationToken
        );

        var result = await _assignmentService.ExpireAssignmentEntryAsync(
            assignment.Id,
            new ExpireShiftRequest { CancellationReason = "done" },
            UserA,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(FixedNow, result.CancelledAt);
        Assert.Equal(UserA, result.CancelledByUserId);
    }

    [Fact]
    public async Task CreateAssignmentEntryAsync_WhenUniquenessConflicts_RollsBackSerializedWrite()
    {
        _transactionIsolationRecorder.StartedIsolationLevels.Clear();
        await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(8), At(10)),
            TestContext.Current.CancellationToken
        );
        var eventCount = await _db.Events.CountAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assignmentService.CreateAssignmentEntryAsync(
                CreateAssignmentEntryRequest(At(14), At(16)),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(1, await _db.AssignmentEntries.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(eventCount, await _db.Events.CountAsync(TestContext.Current.CancellationToken));
        Assert.All(
            _transactionIsolationRecorder.StartedIsolationLevels,
            isolationLevel => Assert.Equal(IsolationLevel.Serializable, isolationLevel)
        );
        Assert.Equal(2, _transactionIsolationRecorder.StartedIsolationLevels.Count);
    }

    [Fact]
    public async Task LinkShiftSeriesAsync_WhenAnyIntersectionHasInvalidUsers_RollsBackAllLinks()
    {
        var shiftSeries = await _shiftService.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest() with
            {
                RecurrenceRule = "FREQ=DAILY;COUNT=2",
            },
            TestContext.Current.CancellationToken
        );
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest() with
            {
                RecurrenceRule = "FREQ=DAILY;COUNT=2",
            },
            TestContext.Current.CancellationToken
        );
        var secondShift = await _db
            .ShiftEntries.Include(x => x.Users)
            .OrderBy(x => x.Id)
            .LastAsync(TestContext.Current.CancellationToken);
        _db.ShiftEntryUsers.RemoveRange(secondShift.Users);
        secondShift.Users.Add(new ShiftEntryUser { UserId = UserB });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _linkService.LinkShiftSeriesAsync(
                new ShiftAssignmentSeriesRequest
                {
                    ShiftSeriesId = shiftSeries.Id,
                    AssignmentSeriesId = assignmentSeries.Id,
                    AssignedUserIds = [UserA],
                },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Empty(await _db.ShiftAssignmentSeriesLinks.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await _db.ShiftAssignmentEntries.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LinkedEntryUpdates_WhenTheyInvalidateLinks_AreRejectedWithoutMutation()
    {
        var (shift, assignment, _) = await CreateLinkedEntriesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _shiftService.UpdateShiftEntryAsync(
                shift.Id,
                CreateShiftEntryRequest(At(10), At(12)) with
                {
                    UserIds = [UserB],
                },
                TestContext.Current.CancellationToken
            )
        );
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assignmentService.UpdateAssignmentEntryAsync(
                assignment.Id,
                CreateAssignmentEntryUpdateRequest(At(20), At(22)),
                TestContext.Current.CancellationToken
            )
        );

        var storedShift = await _db
            .ShiftEntries.Include(x => x.Users)
            .SingleAsync(x => x.Id == shift.Id, TestContext.Current.CancellationToken);
        var storedAssignment = await _db
            .AssignmentEntries.Include(x => x.Event)
            .SingleAsync(x => x.Id == assignment.Id, TestContext.Current.CancellationToken);
        Assert.Equal([UserA], storedShift.Users.Select(x => x.UserId));
        Assert.Equal(At(10), storedAssignment.Event!.StartAtUtc);
    }

    [Fact]
    public async Task CancelledShiftLink_DoesNotFilterOrBlockAssignmentAndIsNotMappedActive()
    {
        var (shift, assignment, _) = await CreateLinkedEntriesAsync();
        await _shiftService.ExpireShiftEntryAsync(
            shift.Id,
            new ExpireShiftRequest(),
            cancellationToken: TestContext.Current.CancellationToken
        );

        var filtered = await GetCalendarAsync([UserA], includeShifts: false, includeAssignments: true);
        var unfiltered = await GetCalendarAsync(null, includeShifts: false, includeAssignments: true);
        var updated = await _assignmentService.UpdateAssignmentEntryAsync(
            assignment.Id,
            CreateAssignmentEntryUpdateRequest(At(20), At(22)),
            TestContext.Current.CancellationToken
        );

        Assert.Empty(filtered.Events);
        Assert.Empty(Assert.Single(unfiltered.Events).ResourceIds);
        Assert.Equal(At(20), updated!.StartAtUtc);
    }

    private async Task<(ShiftSeriesResponse Shift, AssignmentSeriesResponse Assignment)> CreateLinkedSeriesAsync()
    {
        var shift = await _shiftService.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(),
            TestContext.Current.CancellationToken
        );
        var assignment = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest(),
            TestContext.Current.CancellationToken
        );
        await _linkService.LinkShiftSeriesAsync(
            new ShiftAssignmentSeriesRequest
            {
                ShiftSeriesId = shift.Id,
                AssignmentSeriesId = assignment.Id,
                AssignedUserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );
        return (shift, assignment);
    }

    private async Task<(
        ShiftEntryResponse Shift,
        AssignmentEntryResponse Assignment,
        ShiftAssignmentEntryResponse Link
    )> CreateLinkedEntriesAsync()
    {
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(17)),
            TestContext.Current.CancellationToken
        );
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(10), At(12)),
            TestContext.Current.CancellationToken
        );
        var link = await _linkService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = shift.Id,
                AssignmentEntryId = assignment.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );
        return (shift, assignment, link);
    }

    private Task<SchedulingCalendarDataResponse> GetCalendarAsync(
        IReadOnlyCollection<Guid>? userIds,
        bool includeShifts,
        bool includeAssignments
    ) =>
        _calendarService.GetDataAsync(
            new SchedulingCalendarRequest
            {
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2026, 6, 1),
                TimeZoneId = "UTC",
                UserIds = userIds,
            },
            includeShifts,
            includeAssignments,
            TestContext.Current.CancellationToken
        );

    private async Task SeedBaseDataAsync()
    {
        _db.EventTypes.AddRange(
            CreateEventType(SchedulingConstants.ShiftEventTypeCode),
            CreateEventType(SchedulingConstants.AssignmentEventTypeCode)
        );
        _db.EventStatusTypes.AddRange(
            CreateStatusType(CalendarEventStatusTypeCodes.Draft),
            CreateStatusType(CalendarEventStatusTypeCodes.Active),
            CreateStatusType(CalendarEventStatusTypeCodes.Cancelled)
        );
        _db.Locations.AddRange(
            new Location
            {
                Id = 5,
                AgencyId = "A5",
                Name = "Five",
                Timezone = "America/Vancouver",
            },
            new Location
            {
                Id = 9,
                AgencyId = "A9",
                Name = "Nine",
                Timezone = "America/Toronto",
            }
        );
        _db.Users.AddRange(CreateUser(UserA, "A"), CreateUser(UserB, "B"));
        _db.StatGroups.Add(new StatGroup { Id = 1, Name = "Group" });
        _db.StatCategories.AddRange(
            new StatCategory
            {
                Id = 10,
                GroupId = 1,
                Name = "Category 10",
            },
            new StatCategory
            {
                Id = 20,
                GroupId = 1,
                Name = "Category 20",
            }
        );
        _db.SubCategories.AddRange(
            new SubCategory
            {
                Id = 11,
                CategoryId = 10,
                Name = "Subcategory 11",
            },
            new SubCategory
            {
                Id = 21,
                CategoryId = 20,
                Name = "Subcategory 21",
            }
        );
        _db.AssignmentDefinitions.Add(
            new AssignmentDefinition
            {
                Id = 100,
                LocationId = 5,
                Name = "Definition",
                NormalizedName = "DEFINITION",
                CategoryId = 10,
                SubCategoryId = 11,
                Color = "definition-color",
                DefaultStartTime = new TimeOnly(8, 0),
                DefaultEndTime = new TimeOnly(16, 0),
                DefaultCapacity = 3,
                EffectiveDateUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static AssignmentDefinitionRequest CreateDefinitionRequest() =>
        new()
        {
            LocationId = 5,
            Name = "Definition",
            CategoryId = 10,
            SubCategoryId = 11,
            Color = "definition-color",
            DefaultStartTime = "08:00",
            DefaultEndTime = "16:00",
            DefaultCapacity = 3,
            EffectiveDateUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

    private static AssignmentSeriesRequest CreateAssignmentSeriesRequest() =>
        new()
        {
            AssignmentDefinitionId = 100,
            Title = "Assignment series",
            Color = "request-color",
            RecurrenceRule = "FREQ=DAILY;COUNT=1",
            TimeZoneId = "UTC",
            StartAtUtc = At(10),
            EndAtUtc = At(12),
            LocationId = 5,
            CategoryId = 10,
            SubCategoryId = 11,
            Capacity = 5,
        };

    private static AssignmentEntryRequest CreateAssignmentEntryRequest(
        DateTimeOffset? startAtUtc = null,
        DateTimeOffset? endAtUtc = null
    ) =>
        new()
        {
            AssignmentDefinitionId = 100,
            Title = "Assignment",
            Color = "request-color",
            StartAtUtc = startAtUtc ?? At(10),
            EndAtUtc = endAtUtc ?? At(12),
            TimeZoneId = "UTC",
            LocationId = 5,
            CategoryId = 10,
            SubCategoryId = 11,
            Capacity = 5,
        };

    private static AssignmentEntryUpdateRequest CreateAssignmentEntryUpdateRequest(
        DateTimeOffset? startAtUtc = null,
        DateTimeOffset? endAtUtc = null
    ) =>
        new()
        {
            AssignmentDefinitionId = 100,
            Title = "Assignment",
            Color = "request-color",
            StartAtUtc = startAtUtc ?? At(10),
            EndAtUtc = endAtUtc ?? At(12),
            TimeZoneId = "UTC",
            LocationId = 5,
            CategoryId = 10,
            SubCategoryId = 11,
            Capacity = 5,
        };

    private static ShiftSeriesRequest CreateShiftSeriesRequest() =>
        new()
        {
            Title = "Shift series",
            Color = "blue",
            RecurrenceRule = "FREQ=DAILY;COUNT=1",
            TimeZoneId = "UTC",
            StartAtUtc = At(9),
            EndAtUtc = At(17),
            LocationId = 5,
            UserIds = [UserA],
        };

    private static ShiftEntryRequest CreateShiftEntryRequest(DateTimeOffset startAtUtc, DateTimeOffset endAtUtc) =>
        new()
        {
            Title = "Shift",
            Color = "blue",
            StartAtUtc = startAtUtc,
            EndAtUtc = endAtUtc,
            TimeZoneId = "UTC",
            LocationId = 5,
            UserIds = [UserA],
        };

    private static DateTimeOffset At(int hour) => new(2026, 6, 1, hour, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset AtDay(int day, int hour) => new(2026, 6, day, hour, 0, 0, TimeSpan.Zero);

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

    private static User CreateUser(Guid id, string name) =>
        new()
        {
            Id = id,
            IdirName = name,
            IsEnabled = true,
            FirstName = name,
            LastName = "Test",
            Email = $"{name}@example.com",
            Gender = Gender.Other,
        };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TransactionIsolationRecorder : DbTransactionInterceptor
    {
        public List<IsolationLevel> StartedIsolationLevels { get; } = [];

        public override ValueTask<DbTransaction> TransactionStartedAsync(
            DbConnection connection,
            TransactionEndEventData eventData,
            DbTransaction result,
            CancellationToken cancellationToken = default
        )
        {
            StartedIsolationLevels.Add(result.IsolationLevel);
            return ValueTask.FromResult(result);
        }
    }
}
