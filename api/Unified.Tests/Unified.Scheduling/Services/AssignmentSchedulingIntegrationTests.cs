using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Factories;
using Unified.Calendar.Mappings;
using Unified.Calendar.Models;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Calendar.Scheduling.Models;
using Unified.Common.Time;
using Unified.Common.Validation;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.Scheduling;
using Unified.Db.Models.Stats;
using Unified.Db.Models.UserManagement;
using Unified.LoanOut.Constants;
using Unified.LoanOut.Factories;
using Unified.LoanOut.Guards;
using Unified.LoanOut.Mappers;
using Unified.LoanOut.Models;
using Unified.LoanOut.Services;
using Unified.Scheduling;
using Unified.Scheduling.Factories;
using Unified.Scheduling.Mappings;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.Scheduling.Services;

public sealed class AssignmentSchedulingIntegrationTests : IAsyncLifetime
{
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserB = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserC = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UserD = new("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 15, 19, 30, 0, TimeSpan.Zero);

    private SqliteConnection _connection = null!;
    private UnifiedDbContext _db = null!;
    private AssignmentDefinitionService _definitionService = null!;
    private AssignmentService _assignmentService = null!;
    private ShiftService _shiftService = null!;
    private ShiftAssignmentService _linkService = null!;
    private CalendarConflictService _conflictService = null!;
    private SchedulingCalendarService _calendarService = null!;
    private SchedulingLoanOutService _schedulingLoanOutService = null!;
    private EventSeriesMaterializationService _materializationService = null!;
    private ProposedShiftAssignmentOptionsService _assignmentOptionsService = null!;
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
        _materializationService = materializationService;
        var timeProvider = new FixedTimeProvider(FixedNow);
        _conflictService = new CalendarConflictService(
            [new SchedulingConflictParticipantProvider(_db)],
            _db,
            timeProvider
        );

        _definitionService = new AssignmentDefinitionService(
            NullLogger<AssignmentDefinitionService>.Instance,
            _db,
            timeProvider
        );
        _schedulingLoanOutService = SchedulingLoanOutTestServices.Create(
            _db,
            CreateLoanOutEventMapper(),
            () => _shiftService,
            () => _linkService,
            timeZoneResolver,
            timeZoneService,
            recurrenceExpander
        );
        _linkService = new ShiftAssignmentService(
            NullLogger<ShiftAssignmentService>.Instance,
            _db,
            timeZoneService,
            _conflictService,
            _schedulingLoanOutService,
            SchedulingLoanOutTestServices.CreateAvailability(_db, timeZoneService)
        );
        _assignmentService = CreateAssignment(
            _db,
            materializationService,
            _linkService,
            timeZoneService,
            timeProvider,
            _conflictService
        );
        _shiftService = CreateShift(
            _db,
            materializationService,
            recurrenceExpander,
            _linkService,
            timeZoneService,
            timeProvider,
            _conflictService
        );
        _calendarService = new SchedulingCalendarService(
            NullLogger<SchedulingCalendarService>.Instance,
            _db,
            timeZoneResolver,
            timeZoneService,
            SchedulingLoanOutTestServices.CreateAvailability(_db, timeZoneService, timeZoneResolver)
        );
        _assignmentOptionsService = new ProposedShiftAssignmentOptionsService(
            NullLogger<ProposedShiftAssignmentOptionsService>.Instance,
            _assignmentService,
            recurrenceExpander,
            recurrenceRuleValidator,
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
        var entries = await _assignmentService.GetAssignmentEntriesAsync(
            new AssignmentEntryQueryParams { AssignmentSeriesId = created.Id },
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
        Assert.Equal(result.AssignmentEntryIds, entries.Select(entry => entry.Id));
        Assert.Equal(result.EventIds, entries.Select(entry => entry.EventId));
        Assert.All(
            entries,
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
    public async Task AssignmentEntryExceptionState_CreateAndUpdate_TracksSeriesSlot()
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
            CreateAssignmentEntryUpdateRequest(seriesSlotStart, seriesSlotEnd),
            TestContext.Current.CancellationToken
        );
        Assert.False(aligned!.IsException);
        Assert.Equal(series.Id, aligned.AssignmentSeriesId);

        var moved = await _assignmentService.UpdateAssignmentEntryAsync(
            created.Id,
            CreateAssignmentEntryUpdateRequest(AtDay(2, 11), AtDay(2, 13)),
            TestContext.Current.CancellationToken
        );
        Assert.True(moved!.IsException);
        Assert.Equal(series.Id, moved.AssignmentSeriesId);
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
    public async Task UpdateAssignmentSeriesAsync_WhenRecurrenceChanges_ReconcilesDesiredLinks()
    {
        var (shiftSeries, assignmentSeries) = await CreateLinkedSeriesAsync();

        await _assignmentService.UpdateAssignmentSeriesAsync(
            assignmentSeries.Id,
            CreateAssignmentSeriesRequest() with
            {
                RecurrenceRule = "FREQ=DAILY;COUNT=2",
                ShiftSeriesLinks =
                [
                    new ShiftSeriesLinkRequest { ShiftSeriesId = shiftSeries.Id, AssignedUserIds = [UserA] },
                ],
            },
            TestContext.Current.CancellationToken
        );

        Assert.Single(await _db.ShiftAssignmentSeriesLinks.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAssignmentSeriesAsync_WhenLinksAreOmitted_PreservesExistingLinksAndUsers()
    {
        var (_, assignmentSeries) = await CreateLinkedSeriesAsync();

        _db.ChangeTracker.Clear();

        await _assignmentService.UpdateAssignmentSeriesAsync(
            assignmentSeries.Id,
            CreateAssignmentSeriesRequest() with
            {
                Title = "Updated assignment series",
            },
            TestContext.Current.CancellationToken
        );

        var link = Assert.Single(
            await _db
                .ShiftAssignmentSeriesLinks.Include(existingLink => existingLink.Users)
                .ToListAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal([UserA], link.Users.Select(user => user.UserId));
    }

    [Fact]
    public async Task UpdateAssignmentSeriesAsync_WhenLinksAreOmittedAndTimeChanges_PreservesExistingLinksAndUsers()
    {
        var (_, assignmentSeries) = await CreateLinkedSeriesAsync();

        _db.ChangeTracker.Clear();

        await _assignmentService.UpdateAssignmentSeriesAsync(
            assignmentSeries.Id,
            CreateAssignmentSeriesRequest() with
            {
                StartAtUtc = At(11),
                EndAtUtc = At(13),
            },
            TestContext.Current.CancellationToken
        );

        var link = Assert.Single(
            await _db
                .ShiftAssignmentSeriesLinks.Include(existingLink => existingLink.Users)
                .ToListAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal([UserA], link.Users.Select(user => user.UserId));
    }

    [Fact]
    public async Task UpdateAssignmentSeriesAsync_WhenLinksAreOmittedAndRecurrenceChanges_PreservesExistingLinksAndUsers()
    {
        var (_, assignmentSeries) = await CreateLinkedSeriesAsync();

        _db.ChangeTracker.Clear();

        await _assignmentService.UpdateAssignmentSeriesAsync(
            assignmentSeries.Id,
            CreateAssignmentSeriesRequest() with
            {
                RecurrenceRule = "FREQ=DAILY;COUNT=2",
            },
            TestContext.Current.CancellationToken
        );

        var link = Assert.Single(
            await _db
                .ShiftAssignmentSeriesLinks.Include(existingLink => existingLink.Users)
                .ToListAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal([UserA], link.Users.Select(user => user.UserId));
    }

    [Fact]
    public async Task UpdateAssignmentSeriesAsync_WhenLinksAreEmpty_RemovesExistingLinks()
    {
        var (_, assignmentSeries) = await CreateLinkedSeriesAsync();

        await _assignmentService.UpdateAssignmentSeriesAsync(
            assignmentSeries.Id,
            CreateAssignmentSeriesRequest() with
            {
                ShiftSeriesLinks = [],
            },
            TestContext.Current.CancellationToken
        );

        Assert.Empty(await _db.ShiftAssignmentSeriesLinks.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateShiftSeriesAsync_WhenRecurrenceChanges_ReconcilesDesiredLinks()
    {
        var (shiftSeries, assignmentSeries) = await CreateLinkedSeriesAsync();

        await _shiftService.UpdateShiftSeriesAsync(
            shiftSeries.Id,
            CreateShiftSeriesRequest() with
            {
                RecurrenceRule = "FREQ=DAILY;COUNT=2",
                AssignmentSeriesLinks =
                [
                    new AssignmentSeriesLinkRequest
                    {
                        AssignmentSeriesId = assignmentSeries.Id,
                        AssignedUserIds = [UserA],
                    },
                ],
            },
            TestContext.Current.CancellationToken
        );

        Assert.Single(await _db.ShiftAssignmentSeriesLinks.ToListAsync(TestContext.Current.CancellationToken));
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
    [InlineData(1, 8, 1, 16, 1, 9, 1, 10, true)]
    [InlineData(1, 8, 1, 16, 1, 6, 1, 7, true)]
    [InlineData(1, 8, 1, 16, 1, 14, 1, 18, true)]
    [InlineData(1, 8, 1, 16, 1, 18, 1, 20, true)]
    [InlineData(1, 20, 2, 4, 2, 8, 2, 11, true)]
    [InlineData(1, 20, 2, 4, 3, 8, 3, 11, false)]
    public async Task LinkShiftEntryAsync_RequiresAssignmentStartOnShiftStartOrEndDate(
        int shiftStartDay,
        int shiftStartHour,
        int shiftEndDay,
        int shiftEndHour,
        int assignmentStartDay,
        int assignmentStartHour,
        int assignmentEndDay,
        int assignmentEndHour,
        bool shouldLink
    )
    {
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(AtDay(shiftStartDay, shiftStartHour), AtDay(shiftEndDay, shiftEndHour)),
            TestContext.Current.CancellationToken
        );
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(
                AtDay(assignmentStartDay, assignmentStartHour),
                AtDay(assignmentEndDay, assignmentEndHour)
            ),
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
        Assert.Equal(assignment.AssignmentDefinitionId, assignmentEvent.AssignmentDefinitionId);
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
            CreateShiftEntryRequest(new DateTimeOffset(2026, 5, 31, 23, 0, 0, TimeSpan.Zero), At(0)) with
            {
                UserIds = [UserB],
            },
            TestContext.Current.CancellationToken
        );
        await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(
                new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 2, 1, 0, 0, TimeSpan.Zero)
            ) with
            {
                UserIds = [UserB],
            },
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
    public async Task GetAssignmentSeriesAsync_WhenLaterOccurrenceOverlapsRange_ReturnsSeries()
    {
        var created = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest() with
            {
                RecurrenceRule = "FREQ=DAILY;COUNT=3",
            },
            TestContext.Current.CancellationToken
        );

        var results = await _assignmentService.GetAssignmentSeriesAsync(
            new AssignmentSeriesQueryParams { StartAtUtc = AtDay(2, 9), EndAtUtc = AtDay(2, 13) },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(created.Id, Assert.Single(results).Id);
    }

    [Fact]
    public async Task GetOptionsAsync_WhenAssignmentIsOnSameDayWithoutOverlap_ReturnsOptionAndWarning()
    {
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(10), At(12)),
            TestContext.Current.CancellationToken
        );

        var result = await _assignmentOptionsService.GetOptionsAsync(
            new ProposedShiftAssignmentOptionsRequest
            {
                LocationId = 5,
                StartAtUtc = At(14),
                EndAtUtc = At(16),
                TimeZoneId = "UTC",
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(assignment.Id, Assert.Single(result.EntryOptions).Id);
        Assert.Empty(result.SeriesOptions);
        Assert.True(result.HasSameDayNonOverlappingAssignments);
    }

    [Fact]
    public async Task GetOptionsAsync_WhenAssignmentStartsOnOvernightShiftEndDate_ReturnsOption()
    {
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(AtDay(2, 8), AtDay(2, 11)),
            TestContext.Current.CancellationToken
        );

        var result = await _assignmentOptionsService.GetOptionsAsync(
            new ProposedShiftAssignmentOptionsRequest
            {
                LocationId = 5,
                StartAtUtc = AtDay(1, 20),
                EndAtUtc = AtDay(2, 4),
                TimeZoneId = "UTC",
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(assignment.Id, Assert.Single(result.EntryOptions).Id);
        Assert.True(result.HasSameDayNonOverlappingAssignments);
    }

    [Fact]
    public async Task GetOptionsAsync_WhenAssignmentOverlapsShift_ReturnsOptionWithoutWarning()
    {
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(10), At(12)),
            TestContext.Current.CancellationToken
        );

        var result = await _assignmentOptionsService.GetOptionsAsync(
            new ProposedShiftAssignmentOptionsRequest
            {
                LocationId = 5,
                StartAtUtc = At(11),
                EndAtUtc = At(13),
                TimeZoneId = "UTC",
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(assignment.Id, Assert.Single(result.EntryOptions).Id);
        Assert.False(result.HasSameDayNonOverlappingAssignments);
    }

    [Fact]
    public async Task GetOptionsAsync_WhenAssignmentIsAtDifferentLocation_ExcludesAssignment()
    {
        await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(),
            TestContext.Current.CancellationToken
        );

        var result = await _assignmentOptionsService.GetOptionsAsync(
            new ProposedShiftAssignmentOptionsRequest
            {
                LocationId = 9,
                StartAtUtc = At(9),
                EndAtUtc = At(17),
                TimeZoneId = "UTC",
            },
            TestContext.Current.CancellationToken
        );

        Assert.Empty(result.EntryOptions);
        Assert.Empty(result.SeriesOptions);
    }

    [Fact]
    public async Task GetOptionsAsync_WhenAssignmentIsCancelled_ExcludesAssignment()
    {
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(),
            TestContext.Current.CancellationToken
        );
        await _assignmentService.ExpireAssignmentEntryAsync(
            assignment.Id,
            new ExpireShiftRequest { CancellationReason = "cancelled" },
            UserA,
            TestContext.Current.CancellationToken
        );

        var result = await _assignmentOptionsService.GetOptionsAsync(
            new ProposedShiftAssignmentOptionsRequest
            {
                LocationId = 5,
                StartAtUtc = At(9),
                EndAtUtc = At(17),
                TimeZoneId = "UTC",
            },
            TestContext.Current.CancellationToken
        );

        Assert.Empty(result.EntryOptions);
    }

    [Fact]
    public async Task GetOptionsAsync_WhenWeeklyRecurrenceCrossesDst_ReturnsSeriesOnLaterOccurrenceDate()
    {
        var assignment = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest() with
            {
                RecurrenceRule = "FREQ=DAILY;COUNT=1",
                TimeZoneId = "America/Vancouver",
                StartAtUtc = new DateTimeOffset(2026, 3, 8, 18, 0, 0, TimeSpan.Zero),
                EndAtUtc = new DateTimeOffset(2026, 3, 8, 19, 0, 0, TimeSpan.Zero),
            },
            TestContext.Current.CancellationToken
        );

        var result = await _assignmentOptionsService.GetOptionsAsync(
            new ProposedShiftAssignmentOptionsRequest
            {
                LocationId = 5,
                StartAtUtc = new DateTimeOffset(2026, 3, 1, 17, 0, 0, TimeSpan.Zero),
                EndAtUtc = new DateTimeOffset(2026, 3, 1, 18, 0, 0, TimeSpan.Zero),
                TimeZoneId = "America/Vancouver",
                RecurrenceRule = "FREQ=WEEKLY;COUNT=2",
                IsSeriesScope = true,
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(assignment.Id, Assert.Single(result.SeriesOptions).Id);
    }

    [Fact]
    public async Task GetOptionsAsync_WhenRecurrenceIsUnbounded_Throws()
    {
        var request = new ProposedShiftAssignmentOptionsRequest
        {
            LocationId = 5,
            StartAtUtc = At(9),
            EndAtUtc = At(17),
            TimeZoneId = "UTC",
            RecurrenceRule = "FREQ=WEEKLY",
            IsSeriesScope = true,
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assignmentOptionsService.GetOptionsAsync(request, TestContext.Current.CancellationToken)
        );

        Assert.Contains("bounded", exception.Message);
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

        var exception = await Assert.ThrowsAsync<ConflictValidationException>(() =>
            _assignmentService.CreateAssignmentEntryAsync(
                CreateAssignmentEntryRequest(At(14), At(16)),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("location Five", exception.Message);
        Assert.Contains("assignment definition Definition", exception.Message);
        Assert.Contains("2026-06-01", Assert.Single(exception.Errors["AssignmentDefinitionId"]));
        Assert.Equal(1, await _db.AssignmentEntries.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(eventCount, await _db.Events.CountAsync(TestContext.Current.CancellationToken));
        Assert.All(
            _transactionIsolationRecorder.StartedIsolationLevels,
            isolationLevel => Assert.Equal(IsolationLevel.Serializable, isolationLevel)
        );
        Assert.Equal(2, _transactionIsolationRecorder.StartedIsolationLevels.Count);
    }

    [Fact]
    public async Task CreateAssignmentEntryAsync_WhenDifferentDefinitionsOverlap_AllowsBothAssignments()
    {
        var secondDefinition = await _definitionService.CreateAssignmentDefinitionAsync(
            CreateDefinitionRequest() with
            {
                Name = "Second definition",
            },
            TestContext.Current.CancellationToken
        );
        await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(10), At(13)),
            TestContext.Current.CancellationToken
        );

        var overlapping = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(11), At(12)) with
            {
                AssignmentDefinitionId = secondDefinition.Id,
            },
            TestContext.Current.CancellationToken
        );

        Assert.NotEqual(0, overlapping.Id);
        Assert.Equal(2, await _db.AssignmentEntries.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAssignmentEntriesAsync_WhenActivationCreatesConflict_RollsBackToDraft()
    {
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(12)),
            TestContext.Current.CancellationToken
        );
        await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(10), At(12)) with
            {
                ShiftEntryLinks = [new ShiftEntryLinkRequest { ShiftEntryId = shift.Id, AssignedUserIds = [UserA] }],
            },
            TestContext.Current.CancellationToken
        );
        await _shiftService.PublishShiftEntryAsync(shift.Id, TestContext.Current.CancellationToken);
        var secondAssignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(10), At(12)) with
            {
                AssignmentDefinitionId = 101,
                ShiftEntryLinks = [new ShiftEntryLinkRequest { ShiftEntryId = shift.Id, AssignedUserIds = [UserA] }],
            },
            TestContext.Current.CancellationToken
        );

        var exception = await Assert.ThrowsAsync<CalendarConflictException>(() =>
            _assignmentService.PublishEntriesAsync([secondAssignment.Id], TestContext.Current.CancellationToken)
        );

        Assert.Equal(UserA, Assert.Single(exception.Conflicts).ResourceId);
        _db.ChangeTracker.Clear();
        var stored = await _db
            .AssignmentEntries.Include(entry => entry.Event)
            .SingleAsync(entry => entry.Id == secondAssignment.Id, TestContext.Current.CancellationToken);
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, stored.Event!.StatusTypeCode);
    }

    [Fact]
    public async Task CreateAssignmentEntryAsync_WhenDraftConflicts_PersistsWithStableEventId()
    {
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(8), At(12)),
            TestContext.Current.CancellationToken
        );
        var first = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(8), At(10)) with
            {
                ShiftEntryLinks = [new ShiftEntryLinkRequest { ShiftEntryId = shift.Id, AssignedUserIds = [UserA] }],
            },
            TestContext.Current.CancellationToken
        );
        await _shiftService.PublishShiftEntryAsync(shift.Id, TestContext.Current.CancellationToken);

        var created = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(9), At(11)) with
            {
                AssignmentDefinitionId = 101,
                ShiftEntryLinks = [new ShiftEntryLinkRequest { ShiftEntryId = shift.Id, AssignedUserIds = [UserA] }],
            },
            TestContext.Current.CancellationToken
        );

        Assert.True(created.EventId > 0);
        var exception = await Assert.ThrowsAsync<CalendarConflictException>(() =>
            _assignmentService.PublishAssignmentEntryAsync(created.Id, TestContext.Current.CancellationToken)
        );
        var conflict = Assert.Single(exception.Conflicts);
        Assert.Contains(created.EventId.ToString(), new[] { conflict.Entry.EventId, conflict.Overlaps.EventId });
        _db.ChangeTracker.Clear();

        await _conflictService.CreateOverrideAsync(
            new CalendarConflictAcknowledgement
            {
                FirstSourceModule = conflict.Entry.SourceModule,
                FirstEventId = conflict.Entry.EventId,
                SecondSourceModule = conflict.Overlaps.SourceModule,
                SecondEventId = conflict.Overlaps.EventId,
                ResourceId = conflict.ResourceId,
                Note = "Approved before publication",
            },
            UserA,
            TestContext.Current.CancellationToken
        );
        var published = await _assignmentService.PublishAssignmentEntryAsync(
            created.Id,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(CalendarEventStatusTypeCodes.Active, published!.StatusTypeCode);
    }

    [Fact]
    public async Task PublishShiftEntriesAsync_WhenActivationCreatesConflict_RollsBackToDraft()
    {
        var (shift, firstAssignment, secondAssignment) = await CreateDraftConflictAsync();

        var exception = await Assert.ThrowsAsync<CalendarConflictException>(() =>
            _shiftService.PublishEntriesAsync([shift.Id], TestContext.Current.CancellationToken)
        );

        Assert.Equal(UserA, Assert.Single(exception.Conflicts).ResourceId);
        _db.ChangeTracker.Clear();
        var stored = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .SingleAsync(entry => entry.Id == shift.Id, TestContext.Current.CancellationToken);
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, stored.Event!.StatusTypeCode);
        var assignmentStatuses = await _db
            .AssignmentEntries.Where(entry => entry.Id == firstAssignment.Id || entry.Id == secondAssignment.Id)
            .Select(entry => entry.Event!.StatusTypeCode)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(assignmentStatuses, status => Assert.Equal(CalendarEventStatusTypeCodes.Draft, status));
    }

    [Fact]
    public async Task PublishShiftEntryAsync_WithLinkedDraftAssignment_PublishesAssignmentInSameTransaction()
    {
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(12)),
            TestContext.Current.CancellationToken
        );
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(10), At(11)),
            TestContext.Current.CancellationToken
        );
        await _linkService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = shift.Id,
                AssignmentEntryId = assignment.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );

        var published = await _shiftService.PublishShiftEntryAsync(shift.Id, TestContext.Current.CancellationToken);

        Assert.Equal(CalendarEventStatusTypeCodes.Active, published.StatusTypeCode);
        _db.ChangeTracker.Clear();
        var assignmentStatus = await _db
            .AssignmentEntries.Where(entry => entry.Id == assignment.Id)
            .Select(entry => entry.Event!.StatusTypeCode)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(CalendarEventStatusTypeCodes.Active, assignmentStatus);
    }

    [Fact]
    public async Task SchedulePublishPreviewAsync_WithTwoDraftShiftsOnSameLocalDate_ReturnsShiftBlocker()
    {
        await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(12)),
            TestContext.Current.CancellationToken
        );
        await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(13), At(16)),
            TestContext.Current.CancellationToken
        );

        var preview = await CreateSchedulePublishService()
            .PreviewAsync(CreateSchedulePublishRequest(), TestContext.Current.CancellationToken);

        Assert.False(preview.CanPublish);
        var blocker = Assert.Single(preview.Blockers.ShiftConflicts);
        Assert.Contains("already has a shift", blocker.Message);
    }

    [Fact]
    public async Task SchedulePublishPreviewAsync_WithOverlappingDraftShiftsOnDifferentLocalDates_ReturnsShiftBlocker()
    {
        await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(AtDay(2, 6), AtDay(2, 10)) with
            {
                TimeZoneId = "America/Vancouver",
            },
            TestContext.Current.CancellationToken
        );
        await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(AtDay(2, 8), AtDay(2, 11)) with
            {
                TimeZoneId = "America/Vancouver",
            },
            TestContext.Current.CancellationToken
        );

        var preview = await CreateSchedulePublishService()
            .PreviewAsync(
                new SchedulePublishRequest(5, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 2)),
                TestContext.Current.CancellationToken
            );

        Assert.False(preview.CanPublish);
        var blocker = Assert.Single(preview.Blockers.ShiftConflicts);
        Assert.Contains("overlaps this shift", blocker.Message);
    }

    [Fact]
    public async Task SchedulePublishPreviewAsync_WithDraftShiftConflictingWithPublishedShift_ReturnsShiftBlocker()
    {
        var published = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(12)),
            TestContext.Current.CancellationToken
        );
        await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(13), At(16)),
            TestContext.Current.CancellationToken
        );
        await _shiftService.PublishShiftEntryAsync(published.Id, TestContext.Current.CancellationToken);

        var preview = await CreateSchedulePublishService()
            .PreviewAsync(CreateSchedulePublishRequest(), TestContext.Current.CancellationToken);

        Assert.False(preview.CanPublish);
        Assert.Single(preview.Blockers.ShiftConflicts);
    }

    [Fact]
    public async Task SchedulePublishPreviewAsync_WithLinkedDraftAssignmentConflict_ReturnsAssignmentConflict()
    {
        var publishedShift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(8), At(12)),
            TestContext.Current.CancellationToken
        );
        var publishedAssignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(10), At(12)),
            TestContext.Current.CancellationToken
        );
        await _linkService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = publishedShift.Id,
                AssignmentEntryId = publishedAssignment.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );
        await _shiftService.PublishShiftEntryAsync(publishedShift.Id, TestContext.Current.CancellationToken);

        var draftShift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(13)),
            TestContext.Current.CancellationToken
        );
        var draftAssignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(11), At(13)) with
            {
                AssignmentDefinitionId = 101,
            },
            TestContext.Current.CancellationToken
        );
        await _linkService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = draftShift.Id,
                AssignmentEntryId = draftAssignment.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );

        var preview = await CreateSchedulePublishService()
            .PreviewAsync(CreateSchedulePublishRequest(), TestContext.Current.CancellationToken);

        var conflict = Assert.Single(preview.Blockers.Conflicts);
        string[] conflictEventIds = [conflict.FirstEventId, conflict.SecondEventId];
        Assert.Contains(draftAssignment.EventId.ToString(), conflictEventIds);
        Assert.DoesNotContain(draftShift.EventId.ToString(), conflictEventIds);
        Assert.False(preview.CanPublish);
    }

    [Fact]
    public async Task SchedulePublishPreviewAsync_WithLinkedUsers_DoesNotReturnUnassignedAssignmentWarning()
    {
        var (_, assignment, _) = await CreateLinkedEntriesAsync();

        var preview = await CreateSchedulePublishService()
            .PreviewAsync(CreateSchedulePublishRequest(), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(preview.Warnings.UnassignedAssignments, warning => warning.EventId == assignment.EventId);
    }

    [Fact]
    public async Task SchedulePublishAsync_WhenShiftBlockerAppearsAfterPreview_RechecksAndLeavesDraftsUnchanged()
    {
        var first = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(12)),
            TestContext.Current.CancellationToken
        );
        var service = CreateSchedulePublishService();
        var preview = await service.PreviewAsync(CreateSchedulePublishRequest(), TestContext.Current.CancellationToken);
        Assert.True(preview.CanPublish);
        var second = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(13), At(16)),
            TestContext.Current.CancellationToken
        );

        await Assert.ThrowsAsync<SchedulePublishBlockedException>(() =>
            service.PublishAsync(CreateSchedulePublishRequest(), UserA, TestContext.Current.CancellationToken)
        );

        _db.ChangeTracker.Clear();
        var statuses = await _db
            .Events.Where(eventEntity => eventEntity.Id == first.EventId || eventEntity.Id == second.EventId)
            .Select(eventEntity => eventEntity.StatusTypeCode)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(statuses, status => Assert.Equal(CalendarEventStatusTypeCodes.Draft, status));
    }

    [Fact]
    public async Task SchedulePublishPreviewAsync_OnDstFallbackDay_IgnoresRequestedTimeZoneAndMatchesLocationBoundaries()
    {
        // Use a historical transition because B.C. stopped observing the autumn fallback in 2026.
        var rangeStartUtc = new DateTimeOffset(2025, 11, 2, 7, 0, 0, TimeSpan.Zero);
        var rangeEndUtc = new DateTimeOffset(2025, 11, 3, 8, 0, 0, TimeSpan.Zero);
        var shifts = new[]
        {
            (Start: rangeStartUtc.AddMinutes(-30), End: rangeStartUtc.AddMinutes(30), UserId: UserA),
            (Start: rangeStartUtc.AddHours(-1), End: rangeStartUtc, UserId: UserB),
            (Start: rangeEndUtc.AddMinutes(-30), End: rangeEndUtc.AddMinutes(30), UserId: UserC),
            (Start: rangeEndUtc, End: rangeEndUtc.AddHours(1), UserId: UserD),
        };
        var shiftEntryIds = new List<int>();
        foreach (var shift in shifts)
        {
            var created = await _shiftService.CreateShiftEntryAsync(
                CreateShiftEntryRequest(shift.Start, shift.End) with
                {
                    TimeZoneId = "America/Vancouver",
                    UserIds = [shift.UserId],
                },
                TestContext.Current.CancellationToken
            );
            shiftEntryIds.Add(created.Id);
        }

        var calendar = await _calendarService.GetDataAsync(
            new SchedulingCalendarRequest
            {
                StartDate = new DateOnly(2025, 11, 2),
                EndDate = new DateOnly(2025, 11, 2),
                LocationId = 5,
                TimeZoneId = "America/Toronto",
            },
            includeShifts: true,
            includeAssignments: false,
            includeLeave: false,
            includeLoanOut: false,
            TestContext.Current.CancellationToken
        );
        var preview = await CreateSchedulePublishService()
            .PreviewAsync(
                new SchedulePublishRequest(5, new DateOnly(2025, 11, 2), new DateOnly(2025, 11, 2)),
                TestContext.Current.CancellationToken
            );

        Assert.Equal(2, calendar.Events.Count);
        Assert.Equal([shiftEntryIds[0], shiftEntryIds[2]], calendar.Events.Select(eventItem => eventItem.ShiftEntryId));
        Assert.Equal(calendar.Events.Count, preview.Candidates.ShiftEntryCount);
        Assert.Equal("America/Vancouver", preview.Scope.TimeZoneId);
        Assert.Equal(rangeStartUtc, preview.Scope.RangeStartUtc);
        Assert.Equal(rangeEndUtc, preview.Scope.RangeEndExclusiveUtc);
    }

    [Fact]
    public async Task PublishAssignmentEntriesAsync_WithoutActiveTransaction_CommitsSerializableTransaction()
    {
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(),
            TestContext.Current.CancellationToken
        );
        _transactionIsolationRecorder.StartedIsolationLevels.Clear();

        var result = await _assignmentService.PublishEntriesAsync(
            [assignment.Id],
            TestContext.Current.CancellationToken
        );

        Assert.Equal(assignment.Id, Assert.Single(result).Id);
        Assert.Equal([IsolationLevel.Serializable], _transactionIsolationRecorder.StartedIsolationLevels);
        Assert.Null(_db.Database.CurrentTransaction);
        _db.ChangeTracker.Clear();
        Assert.Equal(
            CalendarEventStatusTypeCodes.Active,
            await _db
                .AssignmentEntries.Where(entry => entry.Id == assignment.Id)
                .Select(entry => entry.Event!.StatusTypeCode)
                .SingleAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task PublishShiftEntriesAsync_WithoutActiveTransaction_CommitsSerializableTransaction()
    {
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(12)),
            TestContext.Current.CancellationToken
        );
        _transactionIsolationRecorder.StartedIsolationLevels.Clear();

        var result = await _shiftService.PublishEntriesAsync([shift.Id], TestContext.Current.CancellationToken);

        Assert.Equal(shift.Id, Assert.Single(result.ShiftEntries).Id);
        Assert.Equal([IsolationLevel.Serializable], _transactionIsolationRecorder.StartedIsolationLevels);
        Assert.Null(_db.Database.CurrentTransaction);
        _db.ChangeTracker.Clear();
        Assert.Equal(
            CalendarEventStatusTypeCodes.Active,
            await _db
                .ShiftEntries.Where(entry => entry.Id == shift.Id)
                .Select(entry => entry.Event!.StatusTypeCode)
                .SingleAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task PublishEntriesAsync_WithAmbientTransaction_DoesNotCreateOrCommitTransaction()
    {
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(12)),
            TestContext.Current.CancellationToken
        );
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(13), At(14)),
            TestContext.Current.CancellationToken
        );
        _transactionIsolationRecorder.StartedIsolationLevels.Clear();

        await using (
            var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                TestContext.Current.CancellationToken
            )
        )
        {
            await _shiftService.PublishEntriesAsync([shift.Id], TestContext.Current.CancellationToken);
            await _assignmentService.PublishEntriesAsync([assignment.Id], TestContext.Current.CancellationToken);

            Assert.Same(transaction, _db.Database.CurrentTransaction);
            Assert.Equal([IsolationLevel.Serializable], _transactionIsolationRecorder.StartedIsolationLevels);
        }

        _db.ChangeTracker.Clear();
        var statuses = await _db
            .Events.Where(eventEntity => eventEntity.Id == shift.EventId || eventEntity.Id == assignment.EventId)
            .Select(eventEntity => eventEntity.StatusTypeCode)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, statuses.Count);
        Assert.All(statuses, status => Assert.Equal(CalendarEventStatusTypeCodes.Draft, status));
    }

    [Fact]
    public async Task PublishEntriesAsync_WithLinkedDraftAssignment_MatchesSingleEntryPublicationBehavior()
    {
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(12)),
            TestContext.Current.CancellationToken
        );
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(10), At(11)),
            TestContext.Current.CancellationToken
        );
        await _linkService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = shift.Id,
                AssignmentEntryId = assignment.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );

        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            TestContext.Current.CancellationToken
        );
        var result = await _shiftService.PublishEntriesAsync([shift.Id], TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(shift.Id, Assert.Single(result.ShiftEntries).Id);
        Assert.Equal([assignment.EventId], result.PublishedLinkedAssignmentEventIds);
        _db.ChangeTracker.Clear();
        var statuses = await _db
            .Events.Where(eventEntity => eventEntity.Id == shift.EventId || eventEntity.Id == assignment.EventId)
            .Select(eventEntity => eventEntity.StatusTypeCode)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(statuses, status => Assert.Equal(CalendarEventStatusTypeCodes.Active, status));
    }

    [Fact]
    public async Task LinkShiftEntryAsync_WhenDraftAssignmentConflicts_PersistsWithoutOverride()
    {
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(8), At(12)),
            TestContext.Current.CancellationToken
        );
        var first = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(8), At(10)),
            TestContext.Current.CancellationToken
        );
        await _linkService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = shift.Id,
                AssignmentEntryId = first.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );
        await _shiftService.PublishShiftEntryAsync(shift.Id, TestContext.Current.CancellationToken);
        var draft = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(9), At(11)) with
            {
                AssignmentDefinitionId = 101,
            },
            TestContext.Current.CancellationToken
        );

        var link = await _linkService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = shift.Id,
                AssignmentEntryId = draft.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(draft.Id, link.AssignmentEntryId);
        Assert.Empty(await _db.CalendarConflictOverrides.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAssignmentEntryAsync_WhenDraftConflicts_CommitsWithoutOverride()
    {
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(8), At(12)),
            TestContext.Current.CancellationToken
        );
        var first = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(8), At(10)),
            TestContext.Current.CancellationToken
        );
        await _linkService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = shift.Id,
                AssignmentEntryId = first.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );
        await _shiftService.PublishShiftEntryAsync(shift.Id, TestContext.Current.CancellationToken);
        var second = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(10), At(12)) with
            {
                AssignmentDefinitionId = 101,
            },
            TestContext.Current.CancellationToken
        );
        await _linkService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = shift.Id,
                AssignmentEntryId = second.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );

        var update = CreateAssignmentEntryUpdateRequest(At(9), At(11)) with { AssignmentDefinitionId = 101 };
        var updated = await _assignmentService.UpdateAssignmentEntryAsync(
            second.Id,
            update,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(At(9), updated!.StartAtUtc);
        Assert.Empty(await _db.CalendarConflictOverrides.ToListAsync(TestContext.Current.CancellationToken));
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
    public async Task LinkShiftSeriesAsync_SkipsOnlyLoanedOutUsersOnLoanOutDates()
    {
        var shiftSeries = await _shiftService.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest() with
            {
                RecurrenceRule = "FREQ=DAILY;COUNT=3",
                UserIds = [UserA, UserB],
            },
            TestContext.Current.CancellationToken
        );
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest() with
            {
                RecurrenceRule = "FREQ=DAILY;COUNT=3",
            },
            TestContext.Current.CancellationToken
        );
        _db.LoanOutEntries.Add(
            new LoanOutEntry
            {
                LoanOutLocationId = 9,
                Event = new Event
                {
                    Title = "User A loan-out",
                    StartAtUtc = AtDay(2, 8),
                    EndAtUtc = AtDay(2, 23),
                    TimeZoneId = "UTC",
                    LocationId = 5,
                    SourceModule = LoanOutConstants.SourceModule,
                    EventTypeCode = LoanOutConstants.LoanOutEventTypeCode,
                    StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
                },
                Users = [new LoanOutEntryUser { UserId = UserA }],
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var loanOutService = SchedulingLoanOutTestServices.Create(
            _db,
            CreateLoanOutEventMapper(),
            () => throw new NotSupportedException(),
            () => throw new NotSupportedException(),
            new CalendarTimeZoneResolver(
                Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "UTC" }),
                new TimeZoneService()
            ),
            new TimeZoneService(),
            CreateLoanOutRecurrenceExpander()
        );
        var loanOutResults = await loanOutService.GetAssignableUserIdsByOccurrenceAsync(
            [UserA],
            [new ShiftAssignmentLoanOutContext(AtDay(1, 10), AtDay(2, 12))],
            null,
            "UTC",
            TestContext.Current.CancellationToken
        );
        Assert.Empty(Assert.Single(loanOutResults));
        var linkService = new ShiftAssignmentService(
            NullLogger<ShiftAssignmentService>.Instance,
            _db,
            new TimeZoneService(),
            _conflictService,
            loanOutService,
            SchedulingLoanOutTestServices.CreateAvailability(_db, new TimeZoneService())
        );
        await linkService.LinkShiftSeriesAsync(
            new ShiftAssignmentSeriesRequest
            {
                ShiftSeriesId = shiftSeries.Id,
                AssignmentSeriesId = assignmentSeries.Id,
                AssignedUserIds = [UserA, UserB],
            },
            TestContext.Current.CancellationToken
        );

        var links = await _db
            .ShiftAssignmentEntries.Include(link => link.Users)
            .Include(link => link.ShiftEntry!)
                .ThenInclude(entry => entry.Event)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, links.Count);
        var loanOutDayLink = Assert.Single(links, link => link.ShiftEntry!.Event!.StartAtUtc == AtDay(2, 9));
        Assert.Equal([UserB], loanOutDayLink.Users.Select(user => user.UserId));
        Assert.All(
            links.Where(link => link.ShiftEntry!.Event!.StartAtUtc != AtDay(2, 9)),
            link => Assert.Equal([UserA, UserB], link.Users.Select(user => user.UserId).Order().ToArray())
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoanOut_PreservesMembership_LinksOnlyAvailableUsers_AndRestoresOnDelete(bool overlappingLoanOut)
    {
        var ct = TestContext.Current.CancellationToken;
        var source = await _shiftService.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest() with
            {
                StartAtUtc = AtDay(1, 9),
                EndAtUtc = AtDay(1, 17),
                RecurrenceRule = "FREQ=DAILY;COUNT=3",
                UserIds = [UserA, UserB],
            },
            ct
        );
        var original = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .SingleAsync(entry => entry.ShiftSeriesId == source.Id && entry.Event!.StartAtUtc == AtDay(2, 9), ct);
        var membershipIds = original.Users.Select(user => user.Id).Order().ToArray();
        var originalEventId = original.EventId;
        var originalEventSeriesId = original.Event!.EventSeriesId;
        var loanOut = CreateLoanOutEntry(AtDay(2, 7));
        loanOut.Event!.EndAtUtc = AtDay(3, 7);
        _db.LoanOutEntries.Add(loanOut);
        LoanOutEntry? remaining = null;
        if (overlappingLoanOut)
        {
            remaining = CreateLoanOutEntry(AtDay(2, 7));
            remaining.Event!.EndAtUtc = AtDay(3, 7);
            _db.LoanOutEntries.Add(remaining);
        }
        await _db.SaveChangesAsync(ct);
        Assert.True(await _schedulingLoanOutService.TransferLoanedOutUserShift(loanOut, cancellationToken: ct));
        Assert.False(await _schedulingLoanOutService.TransferLoanedOutUserShift(loanOut, cancellationToken: ct));
        Assert.Equal(1, await _db.ShiftEntries.CountAsync(entry => entry.Event!.LocationId == 9, ct));
        _db.ChangeTracker.Clear();
        var retained = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .SingleAsync(entry => entry.Id == original.Id, ct);
        Assert.Equal(membershipIds, retained.Users.Select(user => user.Id).Order());
        Assert.Equal(originalEventId, retained.EventId);
        Assert.Equal(originalEventSeriesId, retained.Event!.EventSeriesId);
        Assert.Equal(UserA, Assert.Single(await GetAwayUsersAsync(retained)));
        Assert.False(retained.Event.IsException);
        Assert.Equal(3, await _db.ShiftEntries.CountAsync(entry => entry.ShiftSeriesId == source.Id, ct));
        var calendar = ShiftResponseMapper.ToCalendarEventResponse(retained, await GetAwayUsersAsync(retained));
        Assert.Equal(new[] { UserA, UserB }, calendar.UserIds.Order());
        Assert.Equal([UserB.ToString()], calendar.ResourceIds);

        var assignment = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest() with
            {
                StartAtUtc = AtDay(1, 10),
                EndAtUtc = AtDay(1, 12),
                RecurrenceRule = "FREQ=DAILY;COUNT=3",
                ShiftSeriesLinks =
                [
                    new ShiftSeriesLinkRequest { ShiftSeriesId = source.Id, AssignedUserIds = [UserA, UserB] },
                ],
            },
            ct
        );
        var links = await _db
            .ShiftAssignmentEntries.Include(link => link.Users)
            .Include(link => link.ShiftEntry)
                .ThenInclude(entry => entry!.Event)
            .Where(link => link.AssignmentEntry!.AssignmentSeriesId == assignment.Id)
            .ToListAsync(ct);
        Assert.Equal(3, links.Count);
        Assert.Equal(
            [UserB],
            Assert.Single(links, link => link.ShiftEntryId == retained.Id).Users.Select(user => user.UserId)
        );
        Assert.All(
            links.Where(link => link.ShiftEntryId != retained.Id),
            link => Assert.Equal(new[] { UserA, UserB }, link.Users.Select(user => user.UserId).Order())
        );

        var destination = await _db.ShiftEntries.SingleAsync(entry => entry.Event!.LocationId == 9, ct);
        Assert.True(await _shiftService.DeleteShiftEntryAsync(destination.Id, ct));
        var service = new LoanOutEntryService(
            NullLogger<LoanOutEntryService>.Instance,
            _db,
            new LoanOutEventMapper(new CalendarEventMapper()),
            Entry<LoanOutEntryRequest, LoanOutEntry>(new LoanOutEventFactory()),
            _schedulingLoanOutService
        );
        Assert.True(await service.DeleteLoanOutEntryAsync(loanOut.Id, ct));
        _db.ChangeTracker.Clear();
        retained = await _db
            .ShiftEntries.Include(entry => entry.Users)
            .SingleAsync(entry => entry.Id == original.Id, ct);
        Assert.Equal(overlappingLoanOut, (await GetAwayUsersAsync(retained)).Contains(UserA));
        if (remaining is not null)
        {
            Assert.True(await service.DeleteLoanOutEntryAsync(remaining.Id, ct));
            _db.ChangeTracker.Clear();
            retained = await _db
                .ShiftEntries.Include(entry => entry.Users)
                .SingleAsync(entry => entry.Id == original.Id, ct);
        }
        Assert.Empty(await GetAwayUsersAsync(retained));
        Assert.Equal(membershipIds, retained.Users.Select(user => user.Id).Order());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LinkShiftEntry_DerivesAwayUsersAfterReloadWithoutChangingMembership(bool cancelledLoanOut)
    {
        var ct = TestContext.Current.CancellationToken;
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(AtDay(2, 9), AtDay(2, 17)) with
            {
                UserIds = [UserA, UserB],
            },
            ct
        );
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(AtDay(2, 10), AtDay(2, 12)),
            ct
        );
        await SeedHomeLoanOutValidationAsync(AtDay(2, 7), AtDay(3, 7), cancelledLoanOut);
        _db.ChangeTracker.Clear();

        if (!cancelledLoanOut)
        {
            foreach (var selectedUsers in new[] { new[] { UserA }, new[] { UserA, UserB } })
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    _linkService.LinkShiftEntryAsync(
                        new ShiftAssignmentEntryRequest
                        {
                            ShiftEntryId = shift.Id,
                            AssignmentEntryId = assignment.Id,
                            UserIds = selectedUsers,
                        },
                        ct
                    )
                );
                Assert.Equal("Selected users are away from the linked shift entry.", error.Message);
                Assert.False(await _db.ShiftAssignmentEntries.AnyAsync(ct));
            }
        }
        var availableUsers = cancelledLoanOut ? new[] { UserA, UserB } : new[] { UserB };
        var response = await _linkService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = shift.Id,
                AssignmentEntryId = assignment.Id,
                UserIds = availableUsers,
            },
            ct
        );
        Assert.Equal(availableUsers, response.UserIds);
        Assert.Equal(
            new[] { UserA, UserB },
            await _db
                .ShiftEntryUsers.Where(user => user.ShiftEntryId == shift.Id)
                .OrderBy(user => user.UserId)
                .Select(user => user.UserId)
                .ToArrayAsync(ct)
        );
    }

    [Theory]
    [InlineData("active", true)]
    [InlineData("cancelled", false)]
    [InlineData("previous-day", false)]
    [InlineData("next-day", false)]
    [InlineData("other-user", false)]
    [InlineData("home-fallback", true)]
    [InlineData("other-home", false)]
    [InlineData("other-source", false)]
    public async Task SchedulingCalendar_DerivesAwayUsersAfterReload_WithoutChangingSource(string scenario, bool isAway)
    {
        var ct = TestContext.Current.CancellationToken;
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(AtDay(2, 9), AtDay(2, 17)) with
            {
                UserIds = [UserA, UserB],
            },
            ct
        );
        var membershipIds = await _db
            .ShiftEntryUsers.Where(user => user.ShiftEntryId == shift.Id)
            .OrderBy(user => user.Id)
            .Select(user => user.Id)
            .ToArrayAsync(ct);
        var loanOut = CreateLoanOutEntry(AtDay(2, 7));
        loanOut.Event!.EndAtUtc = AtDay(3, 7);
        if (scenario == "cancelled")
            loanOut.Event.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;
        if (scenario == "previous-day")
        {
            loanOut.Event.StartAtUtc = AtDay(1, 7);
            loanOut.Event.EndAtUtc = AtDay(2, 7);
        }
        if (scenario == "next-day")
        {
            loanOut.Event.StartAtUtc = AtDay(3, 7);
            loanOut.Event.EndAtUtc = AtDay(4, 7);
        }
        if (scenario == "other-user")
            loanOut.Users = [new LoanOutEntryUser { UserId = UserC }];
        if (scenario is "home-fallback" or "other-home")
        {
            loanOut.Event.LocationId = null;
            (await _db.Users.SingleAsync(user => user.Id == UserA, ct)).HomeLocationId =
                scenario == "home-fallback" ? 5 : 9;
            if (scenario == "other-home")
            {
                _db.Locations.Add(
                    new Location
                    {
                        Id = 10,
                        AgencyId = "A10",
                        Name = "Ten",
                        Timezone = "UTC",
                    }
                );
                loanOut.LoanOutLocationId = 10;
            }
        }
        if (scenario == "other-source")
        {
            _db.Locations.Add(
                new Location
                {
                    Id = 10,
                    AgencyId = "A10",
                    Name = "Ten",
                    Timezone = "UTC",
                }
            );
            loanOut.Event.LocationId = 10;
            (await _db.Users.SingleAsync(user => user.Id == UserA, ct)).HomeLocationId = 5;
        }
        _db.LoanOutEntries.Add(loanOut);
        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();
        var request = new SchedulingCalendarRequest
        {
            LocationId = 5,
            StartDate = new DateOnly(2026, 6, 2),
            EndDate = new DateOnly(2026, 6, 2),
        };
        async Task<SchedulingCalendarDataResponse> ReadAsync(IReadOnlyCollection<Guid>? users) =>
            await _calendarService.GetDataAsync(request with { UserIds = users }, true, false, false, false, ct);

        var calendarEvent = Assert.Single((await ReadAsync(null)).Events);
        Assert.Equal(new[] { UserA, UserB }, calendarEvent.UserIds.Order());
        Assert.Equal(
            isAway ? new[] { UserB.ToString() } : new[] { UserA.ToString(), UserB.ToString() },
            calendarEvent.ResourceIds.Order()
        );
        Assert.Equal(isAway ? 0 : 1, (await ReadAsync([UserA])).Events.Count);
        Assert.Single((await ReadAsync([UserB])).Events);
        Assert.False(calendarEvent.IsException);
        Assert.Null(calendarEvent.ExceptionReason);
        var source = await _db
            .ShiftEntries.AsNoTracking()
            .Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .SingleAsync(entry => entry.Id == shift.Id, ct);
        Assert.Equal(membershipIds, source.Users.Select(user => user.Id).Order());
        Assert.False(source.Event!.IsException);
        Assert.Null(source.Event.ExceptionReason);

        if (scenario == "active")
        {
            var persisted = await _db.LoanOutEntries.Include(entry => entry.Event).SingleAsync(ct);
            persisted.Event!.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
            Assert.Equal(
                new[] { UserA.ToString(), UserB.ToString() },
                Assert.Single((await ReadAsync([UserA])).Events).ResourceIds.Order()
            );
        }
    }

    [Theory]
    [InlineData("2026-11-01T06:30:00Z", false)]
    [InlineData("2026-11-01T08:30:00Z", true)]
    [InlineData("2026-11-02T07:30:00Z", true)]
    [InlineData("2026-11-02T08:30:00Z", false)]
    public async Task SchedulingCalendar_DerivesAwayUsersUsingLocationDayAcrossDst(string shiftStart, bool isAway)
    {
        var ct = TestContext.Current.CancellationToken;
        var start = DateTimeOffset.Parse(shiftStart, System.Globalization.CultureInfo.InvariantCulture);
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(start, start.AddMinutes(15)) with
            {
                UserIds = [UserA, UserB],
            },
            ct
        );
        var loanOut = CreateLoanOutEntry(new DateTimeOffset(2026, 11, 1, 7, 0, 0, TimeSpan.Zero));
        loanOut.Event!.EndAtUtc = new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);
        _db.LoanOutEntries.Add(loanOut);
        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();

        var request = new SchedulingCalendarRequest
        {
            LocationId = 5,
            TimeZoneId = "Asia/Tokyo",
            StartDate = new DateOnly(2026, 10, 31),
            EndDate = new DateOnly(2026, 11, 2),
        };
        var result = await _calendarService.GetDataAsync(request, true, false, false, false, ct);
        var calendarEvent = Assert.Single(result.Events);
        Assert.Equal(shift.Id, calendarEvent.ShiftEntryId);
        Assert.Equal(
            isAway ? new[] { UserB.ToString() } : new[] { UserA.ToString(), UserB.ToString() },
            calendarEvent.ResourceIds.Order()
        );
        var filtered = await _calendarService.GetDataAsync(
            request with
            {
                UserIds = [UserA],
            },
            true,
            false,
            false,
            false,
            ct
        );
        Assert.Equal(isAway ? 0 : 1, filtered.Events.Count);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShiftConflicts_DeriveAwayUsersAfterReload_AndStillProtectAvailableUsers(
        bool cancelledLoanOut,
        bool selectAvailableUser
    )
    {
        var ct = TestContext.Current.CancellationToken;
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(AtDay(2, 9), AtDay(2, 17)) with
            {
                UserIds = [UserA, UserB],
            },
            ct
        );
        var existing = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .SingleAsync(entry => entry.Id == shift.Id, ct);
        existing.Event!.StatusTypeCode = CalendarEventStatusTypeCodes.Active;
        await SeedHomeLoanOutValidationAsync(AtDay(2, 7), AtDay(3, 7), cancelledLoanOut);
        _db.ChangeTracker.Clear();
        var candidate = new ShiftEntry
        {
            Event = new Event
            {
                Title = "Candidate",
                StartAtUtc = AtDay(2, 10),
                EndAtUtc = AtDay(2, 12),
                TimeZoneId = "UTC",
                LocationId = 5,
                StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
            },
            Users = [new ShiftEntryUser { UserId = selectAvailableUser ? UserB : UserA }],
        };
        var service = new ShiftConflictService(
            _db,
            new TimeZoneService(),
            SchedulingLoanOutTestServices.CreateAvailability(_db, new TimeZoneService())
        );
        if (cancelledLoanOut || selectAvailableUser)
            await Assert.ThrowsAsync<ConflictValidationException>(() =>
                service.EnsureShiftsDoNotConflictAsync([candidate], [], ct)
            );
        else
            await service.EnsureShiftsDoNotConflictAsync([candidate], [], ct);
        Assert.Equal(
            new[] { UserA, UserB },
            await _db
                .ShiftEntryUsers.Where(user => user.ShiftEntryId == shift.Id)
                .OrderBy(user => user.UserId)
                .Select(user => user.UserId)
                .ToArrayAsync(ct)
        );
    }

    [Theory]
    [InlineData(CalendarEventStatusTypeCodes.Draft, null, true, "IsAway")]
    [InlineData(CalendarEventStatusTypeCodes.Draft, "Other", true, "Other")]
    [InlineData(CalendarEventStatusTypeCodes.Active, null, false, null)]
    [InlineData(CalendarEventStatusTypeCodes.Cancelled, null, false, null)]
    public async Task ApplySeriesLoanOutMarkers_UpdatesOnlyDraftMarkersAndPreservesMembership(
        string status,
        string? originalReason,
        bool expectedException,
        string? expectedReason
    )
    {
        var ct = TestContext.Current.CancellationToken;
        var shift = await _shiftService.CreateShiftEntryAsync(CreateShiftEntryRequest(AtDay(2, 9), AtDay(2, 17)), ct);
        await SeedHomeLoanOutValidationAsync(AtDay(2, 7), AtDay(3, 7));
        var entry = await _db
            .ShiftEntries.Include(item => item.Event)
            .Include(item => item.Users)
            .SingleAsync(item => item.Id == shift.Id, ct);
        var memberships = entry.Users.Select(user => user.Id).Order().ToArray();
        var eventId = entry.EventId;
        entry.Event!.StatusTypeCode = status;
        entry.Event.IsException = originalReason is not null;
        entry.Event.ExceptionReason = originalReason;
        await _db.SaveChangesAsync(ct);

        var service = SchedulingLoanOutTestServices.CreateMarker(_db, new TimeZoneService());
        await service.ApplyToSeriesAsync([entry], ct);
        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();

        var stored = await _db
            .ShiftEntries.Include(item => item.Event)
            .Include(item => item.Users)
            .SingleAsync(item => item.Id == shift.Id, ct);
        Assert.Equal(eventId, stored.EventId);
        Assert.Equal(memberships, stored.Users.Select(user => user.Id).Order());
        Assert.Equal(status, stored.Event!.StatusTypeCode);
        Assert.Equal(expectedException, stored.Event.IsException);
        Assert.Equal(expectedReason, stored.Event.ExceptionReason);
        Assert.Equal(AtDay(2, 9), stored.Event.StartAtUtc);
        Assert.Equal(AtDay(2, 17), stored.Event.EndAtUtc);
        Assert.Equal(5, stored.Event.LocationId);
        Assert.Equal(1, await _db.ShiftEntries.CountAsync(ct));
    }

    [Fact]
    public void AwayStateDoesNotAddDatabaseModelProperties()
    {
        var entity = _db.Model.FindEntityType(typeof(ShiftEntryUser));
        Assert.NotNull(entity);
        Assert.Null(entity.FindProperty("IsAway"));
        Assert.Null(typeof(ShiftEntryUser).GetProperty("IsAway"));
    }

    private async Task<IReadOnlyCollection<Guid>> GetAwayUsersAsync(ShiftEntry entry)
    {
        if (entry.Event is null)
            await _db.Entry(entry).Reference(item => item.Event).LoadAsync(TestContext.Current.CancellationToken);
        var awayUsers = await SchedulingLoanOutTestServices
            .CreateAvailability(_db, new TimeZoneService())
            .GetAwayUserIdsAsync([entry], TestContext.Current.CancellationToken);
        return awayUsers.GetValueOrDefault(entry) ?? [];
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateShiftSeries_DelinksAwayAssignmentUsersBeforeMappingResponses(bool multipleUsers)
    {
        var ct = TestContext.Current.CancellationToken;
        var users = multipleUsers ? new[] { UserA, UserB } : new[] { UserA };
        var request = CreateShiftSeriesRequest() with
        {
            StartAtUtc = AtDay(1, 9),
            EndAtUtc = AtDay(1, 17),
            RecurrenceRule = "FREQ=DAILY;COUNT=3",
            UserIds = users,
        };
        var series = await _shiftService.CreateShiftSeriesAsync(request, ct);
        var shift = await _db
            .ShiftEntries.Include(entry => entry.Users)
            .SingleAsync(entry => entry.ShiftSeriesId == series.Id && entry.Event!.StartAtUtc == AtDay(2, 9), ct);
        var memberIds = shift.Users.Select(user => user.Id).Order().ToArray();
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(AtDay(2, 10), AtDay(2, 12)),
            ct
        );
        await _linkService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = shift.Id,
                AssignmentEntryId = assignment.Id,
                UserIds = users,
            },
            ct
        );
        await SeedHomeLoanOutValidationAsync(AtDay(2, 7), AtDay(3, 7));

        Assert.NotNull(await _shiftService.UpdateShiftSeriesAsync(series.Id, request with { Title = "Updated" }, ct));
        _db.ChangeTracker.Clear();
        var retained = await _db
            .ShiftEntries.Include(entry => entry.Users)
            .SingleAsync(entry => entry.Id == shift.Id, ct);
        Assert.Equal(memberIds, retained.Users.Select(user => user.Id).Order());
        Assert.Contains(UserA, await GetAwayUsersAsync(retained));
        Assert.False(
            await _db.ShiftAssignmentEntryUsers.AnyAsync(
                user => user.ShiftAssignmentEntry!.AssignmentEntryId == assignment.Id && user.UserId == UserA,
                ct
            )
        );
        var response = await _assignmentService.GetAssignmentEntryByIdAsync(assignment.Id, ct);
        Assert.NotNull(response);
        Assert.Equal(multipleUsers ? new[] { UserB } : [], response.AssignedUserIds);
        Assert.Equal(multipleUsers ? 1 : 0, response.AssignedUserCount);
        Assert.True(await _db.AssignmentEntries.AnyAsync(entry => entry.Id == assignment.Id, ct));
    }

    [Theory]
    [InlineData("source", 5)]
    [InlineData("other-source", 5)]
    [InlineData("home-fallback", 5)]
    [InlineData("unknown-home", 5)]
    [InlineData("cancelled", 5)]
    [InlineData("source", 9)]
    public async Task LoanOutAvailabilityAndCreationGuard_RespectSourceLocationPerUser(string scenario, int locationId)
    {
        var ct = TestContext.Current.CancellationToken;
        _db.Locations.Add(
            new Location
            {
                Id = 10,
                AgencyId = "A10",
                Name = "Ten",
                Timezone = "UTC",
            }
        );
        (await _db.Users.SingleAsync(user => user.Id == UserA, ct)).HomeLocationId =
            scenario == "unknown-home" ? null : 5;
        (await _db.Users.SingleAsync(user => user.Id == UserB, ct)).HomeLocationId = 10;
        var loanOut = CreateLoanOutEntry(AtDay(2, 7));
        loanOut.Event!.EndAtUtc = AtDay(3, 7);
        loanOut.Users.Add(new LoanOutEntryUser { UserId = UserB });
        if (scenario == "other-source")
            loanOut.Event.LocationId = 10;
        if (scenario is "home-fallback" or "unknown-home")
            loanOut.Event.LocationId = null;
        if (scenario == "cancelled")
            loanOut.Event.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;
        _db.LoanOutEntries.Add(loanOut);
        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();
        var expected =
            scenario == "source" && locationId == 5 ? new[] { UserC }
            : scenario == "home-fallback" ? new[] { UserB, UserC }
            : new[] { UserA, UserB, UserC };
        var selected = new[] { UserA, UserB, UserC };
        var eligible = await _schedulingLoanOutService.GetAssignableUserIdsByOccurrenceAsync(
            selected,
            [new ShiftAssignmentLoanOutContext(AtDay(2, 10), AtDay(2, 12))],
            locationId,
            "UTC",
            ct
        );
        Assert.Equal(expected, Assert.Single(eligible));

        var zones = new TimeZoneService();
        var provider = new LoanOutCalendarDataProvider(_db, new LoanOutCalendarMapper(), zones);
        var guard = () =>
            global::Unified.Scheduling.Guards.ShiftLoanOutGuard.EnsureCanCreateAsync(
                _db,
                zones,
                provider,
                locationId,
                selected,
                AtDay(2, 10),
                AtDay(2, 12),
                ct
            );
        if (expected.Length < selected.Length)
            await Assert.ThrowsAsync<ConflictValidationException>(guard);
        else
            await guard();
    }

    [Fact]
    public async Task FilterLoanedOutUserAssignments_AllowsDestinationAndExcludesHomeUsers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var loanOut = CreateLoanOutEntry(AtDay(2, 0));
        loanOut.Event!.EndAtUtc = AtDay(3, 0);
        _db.LoanOutEntries.Add(loanOut);
        await _db.SaveChangesAsync(cancellationToken);
        var assignments = new[] { new ShiftAssignmentLoanOutContext(AtDay(2, 10), AtDay(2, 12)) };

        var destination = await _schedulingLoanOutService.GetAssignableUserIdsByOccurrenceAsync(
            [UserA, UserB],
            assignments,
            9,
            "UTC",
            cancellationToken
        );
        var home = await _schedulingLoanOutService.GetAssignableUserIdsByOccurrenceAsync(
            [UserA, UserB],
            assignments,
            5,
            "UTC",
            cancellationToken
        );

        Assert.Equal(new[] { UserA, UserB }, Assert.Single(destination));
        Assert.Equal(UserB, Assert.Single(Assert.Single(home)));
    }

    [Fact]
    public async Task TransferLoanedOutUserShift_SharedSeries_KeepsUsersSpecificToEachDate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _shiftService.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest() with
            {
                StartAtUtc = AtDay(1, 9),
                EndAtUtc = AtDay(1, 17),
                RecurrenceRule = "FREQ=DAILY;COUNT=3",
                UserIds = [UserA, UserB, UserC],
            },
            cancellationToken
        );
        var secondDay = CreateLoanOutEntry(AtDay(2, 10));
        var thirdDay = CreateLoanOutEntry(AtDay(3, 10));
        thirdDay.Users = [new LoanOutEntryUser { UserId = UserB }];
        _db.LoanOutEntries.AddRange(secondDay, thirdDay);
        await _db.SaveChangesAsync(cancellationToken);

        Assert.True(
            await _schedulingLoanOutService.TransferLoanedOutUserShift(
                new LoanOutSeries
                {
                    LoanOutLocationId = 9,
                    EventSeries = new EventSeries
                    {
                        StartAtUtc = AtDay(2, 10),
                        EndAtUtc = AtDay(2, 11),
                        TimeZoneId = "UTC",
                        RecurrenceRule = "FREQ=DAILY;COUNT=2",
                    },
                    LoanOutEntries = [secondDay, thirdDay],
                },
                cancellationToken: cancellationToken
            )
        );

        _db.ChangeTracker.Clear();
        var shifts = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .ToListAsync(cancellationToken);
        Assert.Equal(
            UserA,
            Assert
                .Single(
                    Assert
                        .Single(shifts, entry => entry.Event!.LocationId == 9 && entry.Event.StartAtUtc == AtDay(2, 9))
                        .Users
                )
                .UserId
        );
        Assert.Equal(
            UserB,
            Assert
                .Single(
                    Assert
                        .Single(shifts, entry => entry.Event!.LocationId == 9 && entry.Event.StartAtUtc == AtDay(3, 9))
                        .Users
                )
                .UserId
        );
        var homeSecond = Assert.Single(
            shifts,
            entry =>
                entry.Event!.LocationId == 5
                && entry.Event.StartAtUtc == AtDay(2, 9)
                && entry.Event.ExceptionReason != "IsAway"
        );
        var homeThird = Assert.Single(
            shifts,
            entry =>
                entry.Event!.LocationId == 5
                && entry.Event.StartAtUtc == AtDay(3, 9)
                && entry.Event.ExceptionReason != "IsAway"
        );
        Assert.Equal(new[] { UserA, UserB, UserC }, homeSecond.Users.Select(user => user.UserId).Order());
        Assert.Equal(UserA, Assert.Single(await GetAwayUsersAsync(homeSecond)));
        Assert.Equal(new[] { UserA, UserB, UserC }, homeThird.Users.Select(user => user.UserId).Order());
        Assert.Equal(UserB, Assert.Single(await GetAwayUsersAsync(homeThird)));
        Assert.NotEqual("IsAway", homeSecond.Event!.ExceptionReason);
        Assert.NotEqual("IsAway", homeThird.Event!.ExceptionReason);
        Assert.False(homeSecond.Event.IsException);
        Assert.False(homeThird.Event.IsException);
        var away = shifts
            .Where(entry => entry.Event!.LocationId == 5 && entry.Event.ExceptionReason == "IsAway")
            .ToList();
        Assert.Empty(away);
        Assert.Equal(3, shifts.Count(entry => entry.Event!.LocationId == 5));
    }

    [Fact]
    public async Task TransferLoanedOutUserShift_DelinksAssignmentsWithoutRemovingShiftUsersOrAssignments()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var source = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(AtDay(2, 9), AtDay(2, 17)) with
            {
                UserIds = [UserA],
            },
            cancellationToken
        );
        var other = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(AtDay(2, 9), AtDay(2, 17)) with
            {
                UserIds = [UserB],
            },
            cancellationToken
        );
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(AtDay(2, 10), AtDay(2, 12)),
            cancellationToken
        );
        foreach (var (shiftId, userId) in new[] { (source.Id, UserA), (other.Id, UserB) })
            await _linkService.LinkShiftEntryAsync(
                new ShiftAssignmentEntryRequest
                {
                    ShiftEntryId = shiftId,
                    AssignmentEntryId = assignment.Id,
                    UserIds = [userId],
                },
                cancellationToken
            );

        Assert.True(
            await _schedulingLoanOutService.TransferLoanedOutUserShift(
                CreateLoanOutEntry(AtDay(2, 10)),
                deleteAssignments: true,
                cancellationToken: cancellationToken
            )
        );

        _db.ChangeTracker.Clear();
        Assert.True(await _db.AssignmentEntries.AnyAsync(entry => entry.Id == assignment.Id, cancellationToken));
        var links = await _db
            .ShiftAssignmentEntries.Include(item => item.Users)
            .Where(item => item.AssignmentEntryId == assignment.Id)
            .ToListAsync(cancellationToken);
        var retainedAssignmentLink = Assert.Single(links);
        Assert.Equal(other.Id, retainedAssignmentLink.ShiftEntryId);
        Assert.Equal(UserB, Assert.Single(retainedAssignmentLink.Users).UserId);
        var home = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry => entry.Id == source.Id || entry.Id == other.Id)
            .ToListAsync(cancellationToken);
        var loanedUserEvent = Assert.Single(home, entry => entry.Id == source.Id);
        Assert.Equal(UserA, Assert.Single(loanedUserEvent.Users).UserId);
        Assert.True(loanedUserEvent.Event!.IsException);
        Assert.Equal("IsAway", loanedUserEvent.Event.ExceptionReason);
        var otherUserEvent = Assert.Single(home, entry => entry.Id == other.Id);
        Assert.False(otherUserEvent.Event!.IsException);
        Assert.Null(otherUserEvent.Event.ExceptionReason);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task TransferLoanedOutUserShift_SharedShift_PreservesRemainingUsersAndAssignments(
        bool recurringSource,
        bool deleteAssignments
    )
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var users = new[] { UserA, UserB, UserC };
        int sourceEntryId;
        if (recurringSource)
        {
            var shift = await _shiftService.CreateShiftSeriesAsync(
                CreateShiftSeriesRequest() with
                {
                    StartAtUtc = AtDay(1, 9),
                    EndAtUtc = AtDay(1, 17),
                    RecurrenceRule = "FREQ=DAILY;COUNT=3",
                    UserIds = users,
                },
                cancellationToken
            );
            var assignment = await _assignmentService.CreateAssignmentSeriesAsync(
                CreateAssignmentSeriesRequest() with
                {
                    StartAtUtc = AtDay(1, 10),
                    EndAtUtc = AtDay(1, 12),
                    RecurrenceRule = "FREQ=DAILY;COUNT=3",
                },
                cancellationToken
            );
            await _linkService.LinkShiftSeriesAsync(
                new ShiftAssignmentSeriesRequest
                {
                    ShiftSeriesId = shift.Id,
                    AssignmentSeriesId = assignment.Id,
                    AssignedUserIds = users,
                },
                cancellationToken
            );
            sourceEntryId = await _db
                .ShiftEntries.Where(entry => entry.ShiftSeriesId == shift.Id && entry.Event!.StartAtUtc == AtDay(2, 9))
                .Select(entry => entry.Id)
                .SingleAsync(cancellationToken);
        }
        else
        {
            var shift = await _shiftService.CreateShiftEntryAsync(
                CreateShiftEntryRequest(AtDay(2, 9), AtDay(2, 17)) with
                {
                    UserIds = users,
                },
                cancellationToken
            );
            var assignment = await _assignmentService.CreateAssignmentEntryAsync(
                CreateAssignmentEntryRequest(AtDay(2, 10), AtDay(2, 12)),
                cancellationToken
            );
            await _linkService.LinkShiftEntryAsync(
                new ShiftAssignmentEntryRequest
                {
                    ShiftEntryId = shift.Id,
                    AssignmentEntryId = assignment.Id,
                    UserIds = users,
                },
                cancellationToken
            );
            sourceEntryId = shift.Id;
        }
        var beforeSource = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Include(entry => entry.ShiftAssignmentEntries)
                .ThenInclude(link => link.Users)
            .SingleAsync(entry => entry.Id == sourceEntryId, cancellationToken);
        var membershipIds = beforeSource.Users.Select(user => user.Id).Order().ToArray();
        var beforeLink = Assert.Single(beforeSource.ShiftAssignmentEntries);
        var sourceValues = (
            beforeSource.EventId,
            beforeSource.ShiftSeriesId,
            beforeSource.Event!.EventSeriesId,
            beforeSource.Event.Title,
            beforeSource.Event.StartAtUtc,
            beforeSource.Event.EndAtUtc,
            beforeSource.Event.SeriesStartAtUtc,
            beforeSource.Event.SeriesEndAtUtc,
            beforeSource.Event.LocationId
        );
        var loanOut = CreateLoanOutEntry(AtDay(2, 10));
        loanOut.Users.Add(new LoanOutEntryUser { UserId = UserB });
        _db.LoanOutEntries.Add(loanOut);
        await _db.SaveChangesAsync(cancellationToken);

        Assert.True(
            await _schedulingLoanOutService.TransferLoanedOutUserShift(loanOut, deleteAssignments, cancellationToken)
        );

        _db.ChangeTracker.Clear();
        var source = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .SingleAsync(entry => entry.Id == sourceEntryId, cancellationToken);
        Assert.Equal(users, source.Users.Select(user => user.UserId).Order());
        Assert.Equal(new[] { UserA, UserB }, (await GetAwayUsersAsync(source)).Order());
        Assert.NotEqual("IsAway", source.Event!.ExceptionReason);
        Assert.False(source.Event.IsException);
        var away = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry => entry.Event!.LocationId == 5 && entry.Event.ExceptionReason == "IsAway")
            .ToListAsync(cancellationToken);
        Assert.Empty(away);
        var retainedLink = await _db
            .ShiftAssignmentEntries.Include(link => link.Users)
            .SingleAsync(link => link.ShiftEntryId == sourceEntryId, cancellationToken);
        Assert.Equal(UserC, Assert.Single(retainedLink.Users).UserId);
        Assert.Equal(recurringSource, retainedLink.IsException);
        Assert.Equal(beforeLink.Id, retainedLink.Id);
        Assert.Equal(membershipIds, source.Users.Select(user => user.Id).Order());
        Assert.Equal(
            sourceValues,
            (
                source.EventId,
                source.ShiftSeriesId,
                source.Event!.EventSeriesId,
                source.Event.Title,
                source.Event.StartAtUtc,
                source.Event.EndAtUtc,
                source.Event.SeriesStartAtUtc,
                source.Event.SeriesEndAtUtc,
                source.Event.LocationId
            )
        );
        Assert.True(
            await _db.AssignmentEntries.AnyAsync(entry => entry.Id == retainedLink.AssignmentEntryId, cancellationToken)
        );
        var copied = Assert.Single(
            await _db
                .ShiftEntries.Include(entry => entry.Event)
                .Include(entry => entry.Users)
                .Where(entry => entry.Event!.LocationId == 9)
                .ToListAsync(cancellationToken)
        );
        Assert.Equal(new[] { UserA, UserB }, copied.Users.Select(user => user.UserId).Order());
        Assert.All(
            copied.Users,
            user =>
            {
                Assert.DoesNotContain(user.Id, membershipIds);
            }
        );
        Assert.Null(copied.ShiftSeriesId);
        var untouched = await _db
            .ShiftEntries.Include(entry => entry.Users)
            .Where(entry =>
                entry.Event!.LocationId == 5 && entry.Id != sourceEntryId && entry.Event.ExceptionReason != "IsAway"
            )
            .ToListAsync(cancellationToken);
        Assert.All(untouched, entry => Assert.Equal(users, entry.Users.Select(user => user.UserId).Order()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransferLoanedOutUserShift_SingleEntry_CreatesStandaloneShiftRegardlessOfSourceType(
        bool recurringSource
    )
    {
        if (recurringSource)
            await _shiftService.CreateShiftSeriesAsync(
                CreateShiftSeriesRequest() with
                {
                    StartAtUtc = AtDay(1, 9),
                    EndAtUtc = AtDay(1, 17),
                    RecurrenceRule = "FREQ=DAILY;COUNT=5",
                    UserIds = [UserA],
                },
                TestContext.Current.CancellationToken
            );
        else
            await _shiftService.CreateShiftEntryAsync(
                CreateShiftEntryRequest(AtDay(2, 9), AtDay(2, 17)) with
                {
                    UserIds = [UserA],
                },
                TestContext.Current.CancellationToken
            );

        var transferred = await _schedulingLoanOutService.TransferLoanedOutUserShift(
            CreateLoanOutEntry(AtDay(2, 10)),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.True(transferred);
        var copied = Assert.Single(
            await _db
                .ShiftEntries.AsNoTracking()
                .Include(entry => entry.Event)
                .Include(entry => entry.Users)
                .Where(entry => entry.Event!.LocationId == 9)
                .ToListAsync(TestContext.Current.CancellationToken)
        );
        Assert.Null(copied.ShiftSeriesId);
        Assert.Null(copied.Event!.EventSeriesId);
        Assert.Null(copied.Event.SeriesStartAtUtc);
        Assert.Null(copied.Event.SeriesEndAtUtc);
        Assert.Equal(AtDay(2, 9), copied.Event.StartAtUtc);
        Assert.Equal(AtDay(2, 17), copied.Event.EndAtUtc);
        Assert.Equal([UserA], copied.Users.Select(user => user.UserId));
        Assert.False(
            await _db.ShiftSeries.AnyAsync(
                series => series.EventSeries!.LocationId == 9,
                TestContext.Current.CancellationToken
            )
        );
        var sourceEvents = await _db
            .ShiftEntries.AsNoTracking()
            .Where(entry => entry.Event!.LocationId == 5)
            .Select(entry => entry.Event!)
            .ToListAsync(TestContext.Current.CancellationToken);
        var source = Assert.Single(sourceEvents, eventEntity => eventEntity.StartAtUtc == AtDay(2, 9));
        Assert.True(source.IsException);
        Assert.Equal("IsAway", source.ExceptionReason);
        Assert.All(
            sourceEvents.Where(eventEntity => eventEntity.Id != source.Id),
            eventEntity => Assert.False(eventEntity.IsException)
        );
    }

    [Fact]
    public async Task TransferLoanedOutUserShift_Series_SplitsCopiedSeriesAtGaps()
    {
        var sourceSeries = await _shiftService.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest() with
            {
                StartAtUtc = AtDay(1, 9),
                EndAtUtc = AtDay(1, 17),
                RecurrenceRule = "FREQ=DAILY;COUNT=5",
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );
        var assignment = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest() with
            {
                StartAtUtc = AtDay(1, 10),
                EndAtUtc = AtDay(1, 12),
                RecurrenceRule = "FREQ=DAILY;COUNT=5",
                ShiftSeriesLinks =
                [
                    new ShiftSeriesLinkRequest { ShiftSeriesId = sourceSeries.Id, AssignedUserIds = [UserA] },
                ],
            },
            TestContext.Current.CancellationToken
        );
        var sourceMembershipIds = await _db
            .ShiftEntryUsers.Where(user => user.ShiftEntry!.ShiftSeriesId == sourceSeries.Id)
            .Select(user => user.Id)
            .OrderBy(id => id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        var sourceSeriesMembershipIds = await _db
            .ShiftSeriesUsers.Where(user => user.ShiftSeriesId == sourceSeries.Id)
            .Select(user => user.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        var loanOutSeries = new LoanOutSeries
        {
            LoanOutLocationId = 9,
            EventSeries = new EventSeries
            {
                Title = "Loan-out series",
                StartAtUtc = AtDay(2, 10),
                EndAtUtc = AtDay(2, 11),
                TimeZoneId = "UTC",
                RecurrenceRule = "FREQ=DAILY;COUNT=3",
                LocationId = 5,
                EventTypeCode = LoanOutConstants.LoanOutEventTypeCode,
                StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
            },
            LoanOutEntries = [CreateLoanOutEntry(AtDay(2, 10)), CreateLoanOutEntry(AtDay(4, 10))],
        };
        _db.LoanOutSeries.Add(loanOutSeries);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var loanOutService = SchedulingLoanOutTestServices.Create(
            _db,
            CreateLoanOutEventMapper(),
            () => _shiftService,
            () => _linkService,
            new CalendarTimeZoneResolver(
                Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "UTC" }),
                new TimeZoneService()
            ),
            new TimeZoneService(),
            CreateLoanOutRecurrenceExpander()
        );

        await loanOutService.TransferLoanedOutUserShift(
            loanOutSeries,
            cancellationToken: TestContext.Current.CancellationToken
        );

        var copiedSeries = await _db
            .ShiftSeries.Include(series => series.EventSeries)
            .Include(series => series.Users)
            .Include(series => series.ShiftEntries)
                .ThenInclude(entry => entry.Users)
            .Include(series => series.ShiftEntries)
                .ThenInclude(entry => entry.Event)
            .Where(series => series.EventSeries!.LocationId == 9)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            sourceMembershipIds,
            await _db
                .ShiftEntryUsers.Where(user => user.ShiftEntry!.ShiftSeriesId == sourceSeries.Id)
                .Select(user => user.Id)
                .OrderBy(id => id)
                .ToArrayAsync(TestContext.Current.CancellationToken)
        );
        var sourceAssignmentLinks = await _db
            .ShiftAssignmentEntries.Include(link => link.Users)
            .Include(link => link.ShiftEntry)
                .ThenInclude(entry => entry!.Event)
            .Where(link => link.AssignmentEntry!.AssignmentSeriesId == assignment.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(5, sourceAssignmentLinks.Count);
        Assert.All(
            sourceAssignmentLinks,
            link =>
            {
                var awayDate =
                    link.ShiftEntry!.Event!.StartAtUtc == AtDay(2, 9)
                    || link.ShiftEntry.Event.StartAtUtc == AtDay(4, 9);
                Assert.Equal(awayDate ? 0 : 1, link.Users.Count);
                Assert.Equal(awayDate, link.IsException);
            }
        );
        Assert.Equal(
            5,
            await _db.AssignmentEntries.CountAsync(
                entry => entry.AssignmentSeriesId == assignment.Id,
                TestContext.Current.CancellationToken
            )
        );
        Assert.All(
            copiedSeries,
            series =>
            {
                Assert.NotEqual(sourceSeries.Id, series.Id);
                var member = Assert.Single(series.Users);
                Assert.Equal(UserA, member.UserId);
                Assert.DoesNotContain(member.Id, sourceSeriesMembershipIds);
                Assert.All(
                    series.ShiftEntries,
                    entry =>
                    {
                        var user = Assert.Single(entry.Users);
                        Assert.Equal(UserA, user.UserId);
                        Assert.DoesNotContain(user.Id, sourceMembershipIds);
                    }
                );
            }
        );
        Assert.Equal(2, copiedSeries.Count);
        var copiedEntries = copiedSeries.SelectMany(series => series.ShiftEntries).ToList();
        Assert.Equal(
            new[] { AtDay(2, 9), AtDay(4, 9) },
            copiedEntries.Select(entry => entry.Event!.StartAtUtc).Order()
        );
        Assert.All(copiedSeries, series => Assert.Equal(series.Id, Assert.Single(series.ShiftEntries).ShiftSeriesId));
        Assert.All(copiedEntries, entry => Assert.Equal(9, entry.Event!.LocationId));
    }

    private static SchedulingLoanOutEventMapper CreateLoanOutEventMapper()
    {
        var timeZoneService = new TimeZoneService();
        return new SchedulingLoanOutEventMapper(
            new CalendarTimeZoneResolver(
                Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "UTC" }),
                timeZoneService
            ),
            timeZoneService
        );
    }

    private static IRecurrenceExpander CreateLoanOutRecurrenceExpander()
    {
        var timeZoneService = new TimeZoneService();
        return new IcalNetRecurrenceExpander(
            timeZoneService,
            new CalendarTimeZoneResolver(
                Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "UTC" }),
                timeZoneService
            )
        );
    }

    [Theory]
    [InlineData("other-user")]
    [InlineData("other-day")]
    [InlineData("empty")]
    public async Task HasShiftAtLoanOutLocationAsync_NonMatchingShift_ReturnsFalse(string scenario)
    {
        var ct = TestContext.Current.CancellationToken;
        await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(AtDay(2, 14), AtDay(2, 16)) with
            {
                LocationId = 9,
                UserIds = [UserA],
            },
            ct
        );
        var loanOut = CreateLoanOutEntry(AtDay(scenario == "other-day" ? 3 : 2, 10));
        if (scenario == "other-user")
            loanOut.Users = [new LoanOutEntryUser { UserId = UserB }];
        IReadOnlyCollection<LoanOutEntry> entries = scenario == "empty" ? [] : [loanOut];

        Assert.False(await _schedulingLoanOutService.HasShiftAtLoanOutLocationAsync(entries, 9, ct));
        await ShiftOverlapGuard.EnsureNoShiftAtLoanOutLocationAsync(_schedulingLoanOutService, entries, 9, ct);
    }

    [Fact]
    public async Task EnsureNoShiftAtLoanOutLocationAsync_EntryWithMatchingShift_ThrowsConflict()
    {
        await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(AtDay(2, 14), AtDay(2, 16)) with
            {
                LocationId = 9,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );

        var exception = await Assert.ThrowsAsync<ConflictValidationException>(() =>
            ShiftOverlapGuard.EnsureNoShiftAtLoanOutLocationAsync(
                _schedulingLoanOutService,
                [CreateLoanOutEntry(AtDay(2, 10))],
                9,
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(
            "A shift exists at the loan-out location, so the loan-out cannot be updated or removed.",
            exception.Message
        );
    }

    [Fact]
    public async Task EnsureNoShiftAtLoanOutLocationAsync_SeriesWithMatchingShift_ThrowsConflict()
    {
        await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(AtDay(4, 14), AtDay(4, 16)) with
            {
                LocationId = 9,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );

        var series = new LoanOutSeries
        {
            LoanOutLocationId = 9,
            LoanOutEntries = [CreateLoanOutEntry(AtDay(2, 10)), CreateLoanOutEntry(AtDay(4, 10))],
        };
        var exception = await Assert.ThrowsAsync<ConflictValidationException>(() =>
            ShiftOverlapGuard.EnsureNoShiftAtLoanOutLocationAsync(
                _schedulingLoanOutService,
                series.LoanOutEntries,
                series.LoanOutLocationId,
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(
            "A shift exists at the loan-out location, so the loan-out cannot be updated or removed.",
            exception.Message
        );
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task HomeLoanOutValidation_ConflictingShift_RejectsWholeRequest(bool series, bool multipleUsers)
    {
        var ct = TestContext.Current.CancellationToken;
        var users = multipleUsers ? new[] { UserB, UserA } : new[] { UserA };
        var entryRequest = CreateShiftEntryRequest(AtDay(2, 9), AtDay(2, 17)) with { UserIds = users };
        var seriesRequest = CreateShiftSeriesRequest() with
        {
            StartAtUtc = entryRequest.StartAtUtc,
            EndAtUtc = entryRequest.EndAtUtc,
            RecurrenceRule = "FREQ=DAILY;COUNT=3",
            UserIds = users,
        };
        await SeedHomeLoanOutValidationAsync(AtDay(2, 0), AtDay(3, 0));
        var before = await _db.ShiftEntries.CountAsync(ct);
        var beforeSeries = await _db.ShiftSeries.CountAsync(ct);

        var exception = await Assert.ThrowsAsync<ConflictValidationException>(async () =>
        {
            if (series)
                await _shiftService.CreateShiftSeriesAsync(seriesRequest, ct);
            else
                await _shiftService.CreateShiftEntryAsync(entryRequest, ct);
        });

        Assert.Equal(
            series
                ? "One or more selected employees are loaned out during the selected dates. A shift cannot be created at their home location during that period."
                : "Employee is loaned out. A shift cannot be created .",
            Assert.Single(exception.Errors["LoanOut"])
        );
        _db.ChangeTracker.Clear();
        Assert.Equal(before, await _db.ShiftEntries.CountAsync(ct));
        Assert.Equal(beforeSeries, await _db.ShiftSeries.CountAsync(ct));
        Assert.False(await _db.Events.AnyAsync(item => item.Title == "Rejected update", ct));
    }

    [Fact]
    public async Task HomeLoanOutValidation_ReturnsGenericMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var dayStart = new DateTimeOffset(2026, 10, 26, 7, 0, 0, TimeSpan.Zero);
        await SeedHomeLoanOutValidationAsync(dayStart, dayStart.AddDays(1));
        var user = await _db.Users.SingleAsync(user => user.Id == UserA, ct);
        user.FirstName = "john";
        user.LastName = "Doe";
        var destination = await _db.Locations.SingleAsync(location => location.Id == 9, ct);
        destination.Name = "Provincial Programs";
        await _db.SaveChangesAsync(ct);

        var exception = await Assert.ThrowsAsync<ConflictValidationException>(() =>
            _shiftService.CreateShiftEntryAsync(
                CreateShiftEntryRequest(dayStart.AddHours(9), dayStart.AddHours(17)) with
                {
                    TimeZoneId = "America/Vancouver",
                },
                ct
            )
        );

        Assert.Equal("Employee is loaned out. A shift cannot be created .", Assert.Single(exception.Errors["LoanOut"]));
        _db.ChangeTracker.Clear();
        Assert.False(await _db.ShiftEntries.AnyAsync(ct));
    }

    [Theory]
    [InlineData(false, "destination")]
    [InlineData(true, "destination")]
    [InlineData(false, "cancelled")]
    [InlineData(true, "cancelled")]
    [InlineData(false, "other-user")]
    [InlineData(true, "other-user")]
    [InlineData(false, "outside-range")]
    [InlineData(true, "outside-range")]
    public async Task HomeLoanOutValidation_NonConflictingShift_IsAllowed(bool series, string scenario)
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedHomeLoanOutValidationAsync(
            AtDay(2, 0),
            AtDay(series && scenario == "destination" ? 4 : 3, 0),
            scenario == "cancelled"
        );
        var start = scenario == "outside-range" ? AtDay(4, 9) : AtDay(2, 9);
        var users = scenario == "other-user" ? new[] { UserB } : new[] { UserA };
        var locationId = scenario == "destination" ? 9 : 5;
        if (series)
        {
            var created = await _shiftService.CreateShiftSeriesAsync(
                CreateShiftSeriesRequest() with
                {
                    StartAtUtc = start,
                    EndAtUtc = start.AddHours(8),
                    RecurrenceRule = "FREQ=DAILY;COUNT=2",
                    LocationId = locationId,
                    UserIds = users,
                },
                ct
            );
            var entries = await _db
                .ShiftEntries.Include(entry => entry.Users)
                .Where(entry => entry.ShiftSeriesId == created.Id)
                .ToListAsync(ct);
            Assert.Equal(2, entries.Count);
            Assert.All(entries, entry => Assert.Equal(users, entry.Users.Select(user => user.UserId)));
        }
        else
        {
            var created = await _shiftService.CreateShiftEntryAsync(
                CreateShiftEntryRequest(start, start.AddHours(8)) with
                {
                    LocationId = locationId,
                    UserIds = users,
                },
                ct
            );
            Assert.Equal(users, created.UserIds);
        }
    }

    [Theory]
    [InlineData(3, false)] // Wednesday lies between Tuesday/Friday occurrences, but is not selected.
    [InlineData(5, true)] // Friday must be checked even though it is not the first occurrence.
    public async Task HomeLoanOutValidation_TuesdayFridaySeries_ChecksActualOccurrences(int loanOutDay, bool conflict)
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedHomeLoanOutValidationAsync(AtDay(loanOutDay, 7), AtDay(loanOutDay + 1, 7));
        var request = CreateShiftSeriesRequest() with
        {
            StartAtUtc = AtDay(2, 9),
            EndAtUtc = AtDay(2, 17),
            RecurrenceRule = "FREQ=WEEKLY;BYDAY=TU,FR;COUNT=2",
        };
        if (conflict)
            await Assert.ThrowsAsync<ConflictValidationException>(() =>
                _shiftService.CreateShiftSeriesAsync(request, ct)
            );
        else
        {
            var created = await _shiftService.CreateShiftSeriesAsync(request, ct);
            Assert.Equal(2, await _db.ShiftEntries.CountAsync(entry => entry.ShiftSeriesId == created.Id, ct));
        }
    }

    [Theory]
    [InlineData(3, 1, true)] // June 2 locally, although the UTC date is June 3.
    [InlineData(3, 8, false)] // June 3 locally, after the loan-out ended.
    public async Task HomeLoanOutValidation_UsesHomeLocationDate(int utcDay, int utcHour, bool conflict)
    {
        var ct = TestContext.Current.CancellationToken;
        // Vancouver is UTC-07 in June: this loan-out covers the whole local June 2.
        await SeedHomeLoanOutValidationAsync(AtDay(2, 7), AtDay(3, 7));
        var request = CreateShiftEntryRequest(AtDay(utcDay, utcHour), AtDay(utcDay, utcHour).AddHours(1)) with
        {
            TimeZoneId = "America/Vancouver",
        };
        if (conflict)
            await Assert.ThrowsAsync<ConflictValidationException>(() =>
                _shiftService.CreateShiftEntryAsync(request, ct)
            );
        else
            Assert.NotNull(await _shiftService.CreateShiftEntryAsync(request, ct));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public async Task UpdateShiftSeriesAsync_HomeLoanOutDay_PreservesMembershipAndRefreshesExceptions(
        bool regenerate,
        bool multipleUsers,
        bool transferFirst
    )
    {
        var ct = TestContext.Current.CancellationToken;
        var users = multipleUsers ? new[] { UserA, UserB } : new[] { UserA };
        var request = CreateShiftSeriesRequest() with
        {
            StartAtUtc = AtDay(1, 9),
            EndAtUtc = AtDay(1, 17),
            RecurrenceRule = "FREQ=DAILY;COUNT=3",
            UserIds = users,
        };
        var series = await _shiftService.CreateShiftSeriesAsync(request, ct);
        await SeedHomeLoanOutValidationAsync(AtDay(2, 7), AtDay(3, 7));
        if (transferFirst)
        {
            var loanOut = await _db
                .LoanOutEntries.Include(entry => entry.Event)
                .Include(entry => entry.Users)
                .SingleAsync(ct);
            Assert.True(await _schedulingLoanOutService.TransferLoanedOutUserShift(loanOut, cancellationToken: ct));
        }
        var update = request with { Title = "Updated series", EndAtUtc = regenerate ? AtDay(1, 18) : request.EndAtUtc };
        Assert.NotNull(await _shiftService.UpdateShiftSeriesAsync(series.Id, update, ct));
        Assert.NotNull(await _shiftService.UpdateShiftSeriesAsync(series.Id, update, ct));
        _db.ChangeTracker.Clear();
        var entries = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry => entry.ShiftSeriesId == series.Id)
            .ToListAsync(ct);
        Assert.Equal(3, entries.Count);
        Assert.All(entries, entry => Assert.Equal(users.Order(), entry.Users.Select(user => user.UserId).Order()));
        var affected = Assert.Single(entries, entry => entry.Event!.StartAtUtc == AtDay(2, 9));
        Assert.Equal(UserA, Assert.Single(await GetAwayUsersAsync(affected)));
        Assert.Equal(!multipleUsers, affected.Event!.IsException);
        Assert.Equal(multipleUsers ? null : "IsAway", affected.Event.ExceptionReason);
        foreach (var entry in entries.Except([affected]))
            Assert.Empty(await GetAwayUsersAsync(entry));
        Assert.Equal(regenerate ? AtDay(2, 18) : AtDay(2, 17), affected.Event.EndAtUtc);
        var destinations = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .Where(entry => entry.Event!.LocationId == 9)
            .ToListAsync(ct);
        Assert.Equal(transferFirst ? 1 : 0, destinations.Count);
        if (transferFirst)
            Assert.Equal(AtDay(2, 17), Assert.Single(destinations).Event!.EndAtUtc);
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(0, false)]
    [InlineData(null, false)]
    public async Task HomeLoanOutValidation_RequestEnd_ChecksOvernightDatesWithExclusiveEnd(int? endHour, bool conflict)
    {
        var ct = TestContext.Current.CancellationToken;
        var home = await _db.Locations.SingleAsync(location => location.Id == 5, ct);
        home.Timezone = "UTC";
        await _db.SaveChangesAsync(ct);
        await SeedHomeLoanOutValidationAsync(AtDay(3, 0), AtDay(4, 0));
        var request = CreateShiftEntryRequest(AtDay(2, 22), AtDay(3, 2)) with
        {
            EndAtUtc = endHour.HasValue ? AtDay(3, endHour.Value) : null,
        };
        if (conflict)
        {
            var exception = await Assert.ThrowsAsync<ConflictValidationException>(() =>
                _shiftService.CreateShiftEntryAsync(request, ct)
            );
            Assert.Equal(
                "Employee is loaned out. A shift cannot be created .",
                Assert.Single(exception.Errors["LoanOut"])
            );
        }
        else
            Assert.NotNull(await _shiftService.CreateShiftEntryAsync(request, ct));
    }

    [Theory]
    [InlineData("remove-user")]
    [InlineData("cancelled")]
    [InlineData("outside-range")]
    [InlineData("between-occurrences")]
    public async Task UpdateShiftSeriesAsync_NoLoanOutOnSelectedOccurrences_AllowsEdit(string scenario)
    {
        var ct = TestContext.Current.CancellationToken;
        var request = CreateShiftSeriesRequest() with
        {
            StartAtUtc = AtDay(2, 9),
            EndAtUtc = AtDay(2, 17),
            RecurrenceRule = "FREQ=WEEKLY;BYDAY=TU,FR;COUNT=2",
            UserIds = [UserA, UserB],
        };
        var series = await _shiftService.CreateShiftSeriesAsync(request, ct);
        var loanOutDay =
            scenario == "outside-range" ? 8
            : scenario == "between-occurrences" ? 3
            : 5;
        // Vancouver local midnight is 07:00 UTC in June.
        await SeedHomeLoanOutValidationAsync(AtDay(loanOutDay, 7), AtDay(loanOutDay + 1, 7), scenario == "cancelled");
        var update = request with
        {
            Title = "Allowed update",
            UserIds = scenario == "remove-user" ? [UserB] : request.UserIds,
        };

        Assert.NotNull(await _shiftService.UpdateShiftSeriesAsync(series.Id, update, ct));
        _db.ChangeTracker.Clear();
        var entries = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry => entry.ShiftSeriesId == series.Id)
            .ToListAsync(ct);
        Assert.Equal(2, entries.Count);
        Assert.All(
            entries,
            entry =>
            {
                Assert.Equal(update.Title, entry.Event!.Title);
                Assert.Equal(update.UserIds.Order(), entry.Users.Select(user => user.UserId).Order());
                Assert.False(entry.Event.IsException);
            }
        );
    }

    [Theory]
    [InlineData(false, "none")]
    [InlineData(true, "none")]
    [InlineData(false, "same-user")]
    [InlineData(true, "same-user")]
    [InlineData(false, "other-user")]
    [InlineData(true, "other-user")]
    [InlineData(false, "cancelled")]
    [InlineData(true, "cancelled")]
    [InlineData(false, "other-source")]
    [InlineData(true, "other-source")]
    [InlineData(false, "home-fallback")]
    [InlineData(true, "home-fallback")]
    [InlineData(false, "other-home")]
    [InlineData(true, "other-home")]
    [InlineData(false, "unknown-home")]
    [InlineData(true, "unknown-home")]
    public async Task DeleteLoanOut_ClearsOnlyAffectedAwayFlags(bool series, string overlap)
    {
        var ct = TestContext.Current.CancellationToken;
        var source = await _shiftService.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest() with
            {
                StartAtUtc = AtDay(2, 9),
                EndAtUtc = AtDay(2, 17),
                RecurrenceRule = "FREQ=DAILY;COUNT=5",
                UserIds = [UserA],
            },
            ct
        );
        var otherUser = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(AtDay(2, 9), AtDay(2, 17)) with
            {
                UserIds = [UserB],
            },
            ct
        );
        var shifts = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .Where(entry => entry.ShiftSeriesId == source.Id || entry.Id == otherUser.Id)
            .ToListAsync(ct);
        foreach (var shift in shifts)
        {
            shift.Event!.IsException = true;
            shift.Event.ExceptionReason = shift.Event.StartAtUtc == AtDay(4, 9) ? "Other" : "IsAway";
        }
        var removed = new[] { 2, 5 }
            .Take(series ? 2 : 1)
            .Select(day =>
            {
                var entry = CreateLoanOutEntry(AtDay(day, 7));
                entry.Event!.EndAtUtc = AtDay(day + 1, 7);
                return entry;
            })
            .ToList();
        LoanOutSeries? loanOutSeries = null;
        if (series)
        {
            var eventSeries = new EventSeries
            {
                Title = "Loan out",
                StartAtUtc = AtDay(2, 7),
                EndAtUtc = AtDay(3, 7),
                RecurrenceRule = "FREQ=WEEKLY;BYDAY=TU,FR;COUNT=2",
                TimeZoneId = "UTC",
                LocationId = 5,
                EventTypeCode = LoanOutConstants.LoanOutEventTypeCode,
                StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
            };
            loanOutSeries = new LoanOutSeries
            {
                EventSeries = eventSeries,
                LoanOutLocationId = 9,
                LoanOutEntries = removed,
                Users = [new LoanOutSeriesUser { UserId = UserA }],
            };
            foreach (var entry in removed)
                entry.Event!.EventSeries = eventSeries;
            _db.LoanOutSeries.Add(loanOutSeries);
        }
        else
            _db.LoanOutEntries.AddRange(removed);
        if (overlap != "none")
        {
            var remaining = CreateLoanOutEntry(AtDay(2, 7));
            remaining.Event!.EndAtUtc = AtDay(3, 7);
            if (overlap == "other-user")
                remaining.Users = [new LoanOutEntryUser { UserId = UserB }];
            if (overlap == "cancelled")
                remaining.Event.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;
            if (overlap is "other-source" or "other-home")
                _db.Locations.Add(
                    new Location
                    {
                        Id = 10,
                        AgencyId = "A10",
                        Name = "Ten",
                        Timezone = "UTC",
                    }
                );
            if (overlap == "other-source")
            {
                remaining.Event.LocationId = 10;
                (await _db.Users.SingleAsync(user => user.Id == UserA, ct)).HomeLocationId = 5;
            }
            if (overlap is "home-fallback" or "other-home" or "unknown-home")
            {
                remaining.Event.LocationId = null;
                (await _db.Users.SingleAsync(user => user.Id == UserA, ct)).HomeLocationId =
                    overlap == "home-fallback" ? 5
                    : overlap == "other-home" ? 10
                    : null;
            }
            _db.LoanOutEntries.Add(remaining);
        }
        await _db.SaveChangesAsync(ct);
        var factory = new LoanOutEventFactory();
        var mapper = new CalendarEventMapper();
        if (series)
        {
            var service = new LoanOutSeriesService(
                NullLogger<LoanOutSeriesService>.Instance,
                _db,
                new LoanOutEventMapper(mapper),
                new CalendarEventSeriesService<LoanOutSeriesRequest, LoanOutSeries>(
                    factory,
                    mapper,
                    new CalendarLifecycleService()
                ),
                _materializationService,
                new LoanOutSeriesMaterializationHandler(_db),
                _schedulingLoanOutService
            );
            Assert.True(await service.DeleteLoanOutSeriesAsync(loanOutSeries!.Id, ct));
        }
        else
        {
            var service = new LoanOutEntryService(
                NullLogger<LoanOutEntryService>.Instance,
                _db,
                new LoanOutEventMapper(mapper),
                Entry<LoanOutEntryRequest, LoanOutEntry>(factory),
                _schedulingLoanOutService
            );
            Assert.True(await service.DeleteLoanOutEntryAsync(removed[0].Id, ct));
        }
        _db.ChangeTracker.Clear();
        var after = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .Where(entry => entry.ShiftSeriesId == source.Id)
            .OrderBy(entry => entry.Event!.StartAtUtc)
            .ToListAsync(ct);
        Assert.Equal(5, after.Count);
        var remainsAway = overlap is "same-user" or "home-fallback";
        Assert.Equal(remainsAway, after[0].Event!.IsException);
        Assert.Equal(remainsAway ? "IsAway" : null, after[0].Event!.ExceptionReason);
        Assert.Equal("IsAway", after[1].Event!.ExceptionReason); // Gap between Tuesday and Friday.
        Assert.Equal("Other", after[2].Event!.ExceptionReason);
        Assert.Equal(!series, after[3].Event!.IsException);
        Assert.Equal(series ? null : "IsAway", after[3].Event!.ExceptionReason);
        Assert.Equal("IsAway", after[4].Event!.ExceptionReason); // Outside the deleted occurrences.
        Assert.Equal(
            "IsAway",
            (await _db.ShiftEntries.Include(entry => entry.Event).SingleAsync(entry => entry.Id == otherUser.Id, ct))
                .Event!
                .ExceptionReason
        );
        Assert.Equal(overlap == "none" ? 0 : 1, await _db.LoanOutEntries.CountAsync(ct));
    }

    [Theory]
    [InlineData(false, "covered", true)]
    [InlineData(true, "covered", true)]
    [InlineData(false, "gap", false)]
    [InlineData(true, "gap", false)]
    [InlineData(false, "cancelled", false)]
    [InlineData(true, "cancelled", false)]
    [InlineData(false, "wrong-destination", false)]
    [InlineData(true, "wrong-destination", false)]
    [InlineData(false, "mixed-users", false)]
    [InlineData(true, "mixed-users", false)]
    [InlineData(false, "mixed-local-users", true)]
    [InlineData(true, "mixed-local-users", true)]
    [InlineData(false, "home-user", true)]
    [InlineData(true, "home-user", true)]
    [InlineData(false, "overnight-gap", false)]
    [InlineData(true, "overnight-gap", false)]
    [InlineData(false, "overnight-covered", true)]
    [InlineData(true, "overnight-covered", true)]
    public async Task CreateBorrowedShift_RequiresLoanOutCoverageAndRejectsWithoutPersistingArtifacts(
        bool recurring,
        string scenario,
        bool allowed
    )
    {
        var ct = TestContext.Current.CancellationToken;
        _db.Locations.Add(
            new Location
            {
                Id = 10,
                AgencyId = "A10",
                Name = "Ten",
                Timezone = "UTC",
            }
        );
        (await _db.Users.SingleAsync(user => user.Id == UserA, ct)).HomeLocationId = scenario == "home-user" ? 9 : 5;
        (await _db.Users.SingleAsync(user => user.Id == UserB, ct)).HomeLocationId =
            scenario == "mixed-local-users" ? 9 : 5;
        foreach (var day in scenario == "overnight-covered" ? new[] { 2, 3, 5, 6 } : new[] { 2, 5 })
        {
            var loanOut = CreateLoanOutEntry(AtDay(day, 7));
            loanOut.Event!.EndAtUtc = AtDay(day + 1, 7);
            loanOut.Event.TimeZoneId = "America/Vancouver";
            if (scenario == "cancelled")
                loanOut.Event.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;
            if (scenario == "wrong-destination")
                loanOut.LoanOutLocationId = 10;
            _db.LoanOutEntries.Add(loanOut);
        }
        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();
        var overnight = scenario.StartsWith("overnight", StringComparison.Ordinal);
        var start = AtDay(!recurring && scenario == "gap" ? 3 : 2, overnight ? 22 : 9);
        var end = overnight ? AtDay(3, 6) : start.AddHours(8);
        var userIds = scenario.StartsWith("mixed-", StringComparison.Ordinal)
            ? new[] { UserA, UserB }
            : new[] { UserA };
        var rule = scenario == "gap" ? "FREQ=WEEKLY;BYDAY=TU,WE,FR;COUNT=3" : "FREQ=WEEKLY;BYDAY=TU,FR;COUNT=2";
        async Task CreateAsync()
        {
            if (recurring)
                Assert.NotNull(
                    await _shiftService.CreateShiftSeriesAsync(
                        CreateShiftSeriesRequest() with
                        {
                            LocationId = 9,
                            UserIds = userIds,
                            StartAtUtc = start,
                            EndAtUtc = end,
                            TimeZoneId = "UTC",
                            RecurrenceRule = rule,
                        },
                        ct
                    )
                );
            else
                Assert.NotNull(
                    await _shiftService.CreateShiftEntryAsync(
                        CreateShiftEntryRequest(start, end) with
                        {
                            LocationId = 9,
                            UserIds = userIds,
                            TimeZoneId = "UTC",
                        },
                        ct
                    )
                );
        }
        if (allowed)
            await CreateAsync();
        else
        {
            var error = await Assert.ThrowsAsync<ConflictValidationException>(CreateAsync);
            Assert.Contains("not loaned out to this location", Assert.Single(error.Errors["LoanOut"]));
        }
        _db.ChangeTracker.Clear();
        var entries = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .ToListAsync(ct);
        if (allowed)
        {
            Assert.Equal(recurring ? 2 : 1, entries.Count);
            Assert.All(
                entries,
                entry =>
                {
                    Assert.Equal(9, entry.Event!.LocationId);
                    Assert.Equal(userIds.Order(), entry.Users.Select(user => user.UserId).Order());
                }
            );
            if (recurring)
                Assert.Equal(new[] { 2, 5 }, entries.Select(entry => entry.Event!.StartAtUtc.Day).Order());
        }
        else
        {
            Assert.Empty(entries);
            Assert.False(await _db.ShiftSeries.AnyAsync(ct));
            Assert.False(await _db.ShiftEntryUsers.AnyAsync(ct));
            Assert.False(await _db.ShiftSeriesUsers.AnyAsync(ct));
            Assert.False(await _db.Events.AnyAsync(item => item.SourceModule == SchedulingConstants.SourceModule, ct));
        }
    }

    [Theory]
    [InlineData("covered", true)]
    [InlineData("outside", false)]
    [InlineData("cancelled", false)]
    [InlineData("wrong-destination", false)]
    [InlineData("mixed-users", false)]
    [InlineData("mixed-local-users", true)]
    [InlineData("home-user", true)]
    [InlineData("unknown-home", false)]
    [InlineData("unknown-home-covered", true)]
    [InlineData("unknown-home-new-location", false)]
    [InlineData("unknown-home-return-home", true)]
    [InlineData("overnight-gap", false)]
    [InlineData("overnight-covered", true)]
    [InlineData("exclusive-midnight", true)]
    [InlineData("new-location", false)]
    public async Task UpdateBorrowedShiftEntry_RequiresLoanOutForEverySelectedUserAndLocalDay(
        string scenario,
        bool allowed
    )
    {
        var ct = TestContext.Current.CancellationToken;
        _db.Locations.Add(
            new Location
            {
                Id = 10,
                AgencyId = "A10",
                Name = "Ten",
                Timezone = "UTC",
            }
        );
        (await _db.Users.SingleAsync(user => user.Id == UserA, ct)).HomeLocationId =
            scenario.StartsWith("unknown-home", StringComparison.Ordinal) ? null
            : scenario == "home-user" ? 9
            : 5;
        (await _db.Users.SingleAsync(user => user.Id == UserB, ct)).HomeLocationId =
            scenario == "mixed-local-users" ? 9 : 5;
        var originalLoanOut = CreateLoanOutEntry(AtDay(2, 7));
        originalLoanOut.Event!.EndAtUtc = AtDay(3, 7);
        originalLoanOut.Event.TimeZoneId = "America/Vancouver";
        _db.LoanOutEntries.Add(originalLoanOut);
        await _db.SaveChangesAsync(ct);
        var original = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(AtDay(2, 9), AtDay(2, 17)) with
            {
                LocationId = 9,
                UserIds = [UserA],
            },
            ct
        );
        var membershipIds = await _db
            .ShiftEntryUsers.Where(user => user.ShiftEntryId == original.Id)
            .Select(user => user.Id)
            .ToArrayAsync(ct);
        var loanOut = CreateLoanOutEntry(AtDay(scenario is "outside" or "unknown-home" ? 2 : 3, 7));
        loanOut.Event!.EndAtUtc = loanOut.Event.StartAtUtc.AddDays(1);
        loanOut.Event.TimeZoneId = "America/Vancouver";
        if (scenario == "cancelled")
            loanOut.Event.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;
        if (scenario == "wrong-destination")
            loanOut.LoanOutLocationId = 10;
        _db.LoanOutEntries.Add(loanOut);
        if (scenario == "overnight-covered")
        {
            var next = CreateLoanOutEntry(AtDay(4, 7));
            next.Event!.EndAtUtc = AtDay(5, 7);
            next.Event.TimeZoneId = "America/Vancouver";
            _db.LoanOutEntries.Add(next);
        }
        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();
        var overnight = scenario.StartsWith("overnight", StringComparison.Ordinal) || scenario == "exclusive-midnight";
        var request = new ShiftEntryUpdateRequest
        {
            Title = "Edited borrowed shift",
            LocationId =
                scenario is "new-location" or "unknown-home-new-location" ? 10
                : scenario == "unknown-home-return-home" ? 5
                : 9,
            UserIds = scenario.StartsWith("mixed-", StringComparison.Ordinal) ? [UserA, UserB] : [UserA],
            StartAtUtc = AtDay(3, overnight ? 22 : 9),
            EndAtUtc = overnight ? AtDay(4, scenario == "exclusive-midnight" ? 4 : 6) : AtDay(3, 17),
            TimeZoneId = "UTC",
        };
        if (allowed)
            Assert.NotNull(await _shiftService.UpdateShiftEntryAsync(original.Id, request, ct));
        else
        {
            var error = await Assert.ThrowsAsync<ConflictValidationException>(() =>
                _shiftService.UpdateShiftEntryAsync(original.Id, request, ct)
            );
            Assert.Contains("not loaned out to this location", Assert.Single(error.Errors["LoanOut"]));
        }
        _db.ChangeTracker.Clear();
        var stored = await _db.ShiftEntries.Include(entry => entry.Event).Include(entry => entry.Users).SingleAsync(ct);
        Assert.Equal(original.Id, stored.Id);
        Assert.Equal(original.EventId, stored.EventId);
        Assert.Equal(allowed ? request.StartAtUtc : original.StartAtUtc, stored.Event!.StartAtUtc);
        Assert.Equal(allowed ? request.EndAtUtc : original.EndAtUtc, stored.Event.EndAtUtc);
        Assert.Equal(
            allowed ? request.UserIds.Order() : new[] { UserA },
            stored.Users.Select(user => user.UserId).Order()
        );
        if (!allowed)
            Assert.Equal(membershipIds, stored.Users.Select(user => user.Id));
    }

    [Theory]
    [InlineData("metadata", true)]
    [InlineData("add-wednesday", false)]
    [InlineData("extend", false)]
    [InlineData("extend-covered", true)]
    [InlineData("cancelled-friday", false)]
    [InlineData("override-gap", false)]
    [InlineData("mixed-users", false)]
    [InlineData("mixed-local-users", true)]
    [InlineData("overnight", false)]
    [InlineData("new-location", false)]
    public async Task UpdateBorrowedShiftSeries_ValidatesActualOccurrencesAndRollsBackInvalidChanges(
        string scenario,
        bool allowed
    )
    {
        var ct = TestContext.Current.CancellationToken;
        _db.Locations.Add(
            new Location
            {
                Id = 10,
                AgencyId = "A10",
                Name = "Ten",
                Timezone = "UTC",
            }
        );
        (await _db.Users.SingleAsync(user => user.Id == UserA, ct)).HomeLocationId = 5;
        (await _db.Users.SingleAsync(user => user.Id == UserB, ct)).HomeLocationId =
            scenario == "mixed-local-users" ? 9 : 5;
        await _db.SaveChangesAsync(ct);
        var request = CreateShiftSeriesRequest() with
        {
            LocationId = 9,
            UserIds = [UserA],
            StartAtUtc = AtDay(2, 9),
            EndAtUtc = AtDay(2, 17),
            RecurrenceRule = "FREQ=WEEKLY;BYDAY=TU,FR;COUNT=2",
            TimeZoneId = "UTC",
        };
        foreach (var day in scenario == "extend-covered" ? new[] { 2, 5, 9 } : new[] { 2, 5 })
        {
            var loanOut = CreateLoanOutEntry(AtDay(day, 7));
            loanOut.Event!.EndAtUtc = AtDay(day + 1, 7);
            loanOut.Event.TimeZoneId = "America/Vancouver";
            _db.LoanOutEntries.Add(loanOut);
        }
        await _db.SaveChangesAsync(ct);
        var series = await _shiftService.CreateShiftSeriesAsync(request, ct);
        if (scenario == "cancelled-friday")
        {
            var friday = await _db
                .LoanOutEntries.Include(entry => entry.Event)
                .SingleAsync(entry => entry.Event!.StartAtUtc == AtDay(5, 7), ct);
            friday.Event!.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;
        }
        if (scenario == "override-gap")
        {
            var occurrence = await _db
                .ShiftEntries.Include(entry => entry.Event)
                .SingleAsync(entry => entry.ShiftSeriesId == series.Id && entry.Event!.StartAtUtc == AtDay(5, 9), ct);
            occurrence.Event!.StartAtUtc = AtDay(4, 9);
            occurrence.Event.EndAtUtc = AtDay(4, 17);
            occurrence.Event.IsException = true;
        }
        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();
        var before = await _db
            .ShiftEntries.AsNoTracking()
            .Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry => entry.ShiftSeriesId == series.Id)
            .OrderBy(entry => entry.Id)
            .ToListAsync(ct);
        var update = request with
        {
            Title = "Edited destination series",
            RecurrenceRule =
                scenario == "add-wednesday" ? "FREQ=WEEKLY;BYDAY=TU,WE,FR;COUNT=3"
                : scenario.StartsWith("extend", StringComparison.Ordinal) ? "FREQ=WEEKLY;BYDAY=TU,FR;COUNT=3"
                : request.RecurrenceRule,
            UserIds = scenario.StartsWith("mixed-", StringComparison.Ordinal) ? [UserA, UserB] : [UserA],
            EndAtUtc = scenario == "overnight" ? AtDay(3, 6) : request.EndAtUtc,
            LocationId = scenario == "new-location" ? 10 : 9,
        };
        if (allowed)
            Assert.NotNull(await _shiftService.UpdateShiftSeriesAsync(series.Id, update, ct));
        else
            await Assert.ThrowsAsync<ConflictValidationException>(() =>
                _shiftService.UpdateShiftSeriesAsync(series.Id, update, ct)
            );
        _db.ChangeTracker.Clear();
        var parent = await _db
            .ShiftSeries.Include(item => item.EventSeries)
            .SingleAsync(item => item.Id == series.Id, ct);
        Assert.Equal(allowed ? update.Title : request.Title, parent.EventSeries!.Title);
        Assert.Equal(allowed ? update.RecurrenceRule : request.RecurrenceRule, parent.EventSeries.RecurrenceRule);
        var after = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry => entry.ShiftSeriesId == series.Id)
            .OrderBy(entry => entry.Id)
            .ToListAsync(ct);
        if (!allowed)
        {
            Assert.Equal(before.Select(entry => entry.Id), after.Select(entry => entry.Id));
            Assert.Equal(
                before.Select(entry =>
                    (entry.EventId, entry.Event!.StartAtUtc, entry.Event.EndAtUtc, entry.Event.LocationId)
                ),
                after.Select(entry =>
                    (entry.EventId, entry.Event!.StartAtUtc, entry.Event.EndAtUtc, entry.Event.LocationId)
                )
            );
            Assert.Equal(
                before.SelectMany(entry => entry.Users).Select(user => (user.Id, user.UserId)),
                after.SelectMany(entry => entry.Users).Select(user => (user.Id, user.UserId))
            );
        }
        else
        {
            Assert.Equal(
                scenario == "extend-covered" ? new[] { 2, 5, 9 } : new[] { 2, 5 },
                after.Select(entry => entry.Event!.StartAtUtc.Day).Order()
            );
            Assert.All(
                after,
                entry => Assert.Equal(update.UserIds.Order(), entry.Users.Select(user => user.UserId).Order())
            );
        }
    }

    [Theory]
    [InlineData("2026-11-02T04:30:00Z", "2026-11-02T05:00:00Z", true)]
    [InlineData("2026-11-02T04:30:00Z", "2026-11-02T05:15:00Z", false)]
    [InlineData("2026-11-02T05:30:00Z", "2026-11-02T06:00:00Z", false)]
    public async Task UpdateBorrowedShift_UsesLoanOutCalendarDatesAcrossTimeZonesAndDst(
        string start,
        string end,
        bool allowed
    )
    {
        var ct = TestContext.Current.CancellationToken;
        (await _db.Users.SingleAsync(user => user.Id == UserA, ct)).HomeLocationId = 5;
        var originalLoanOut = CreateLoanOutEntry(AtDay(2, 7));
        originalLoanOut.Event!.EndAtUtc = AtDay(3, 7);
        originalLoanOut.Event.TimeZoneId = "America/Vancouver";
        _db.LoanOutEntries.Add(originalLoanOut);
        await _db.SaveChangesAsync(ct);
        var original = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(AtDay(2, 9), AtDay(2, 17)) with
            {
                LocationId = 9,
            },
            ct
        );
        var loanOut = CreateLoanOutEntry(new DateTimeOffset(2026, 11, 1, 7, 0, 0, TimeSpan.Zero));
        loanOut.Event!.EndAtUtc = new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);
        loanOut.Event.TimeZoneId = "America/Vancouver";
        _db.LoanOutEntries.Add(loanOut);
        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();
        var update = new ShiftEntryUpdateRequest
        {
            Title = "DST edit",
            LocationId = 9,
            UserIds = [UserA],
            TimeZoneId = "UTC",
            StartAtUtc = DateTimeOffset.Parse(start, System.Globalization.CultureInfo.InvariantCulture),
            EndAtUtc = DateTimeOffset.Parse(end, System.Globalization.CultureInfo.InvariantCulture),
        };
        if (allowed)
            Assert.NotNull(await _shiftService.UpdateShiftEntryAsync(original.Id, update, ct));
        else
            await Assert.ThrowsAsync<ConflictValidationException>(() =>
                _shiftService.UpdateShiftEntryAsync(original.Id, update, ct)
            );
    }

    [Fact]
    public async Task DestinationShiftGuard_WhenLoanOutProviderIsDisabled_AllowsUpdate()
    {
        var ct = TestContext.Current.CancellationToken;
        (await _db.Users.SingleAsync(user => user.Id == UserA, ct)).HomeLocationId = 5;
        await _db.SaveChangesAsync(ct);
        await global::Unified.Scheduling.Guards.LoanOutUserShiftGuard.EnsureCanScheduleAsync(
            _db,
            new TimeZoneService(),
            null,
            9,
            [UserA],
            AtDay(3, 9),
            AtDay(3, 17),
            ct
        );
    }

    [Theory]
    [InlineData(false, false, 3, false)]
    [InlineData(false, true, 3, false)]
    [InlineData(false, false, 4, false)]
    [InlineData(true, false, 3, false)]
    [InlineData(true, true, 3, false)]
    [InlineData(true, false, 4, false)]
    [InlineData(false, false, 4, true)]
    [InlineData(true, false, 4, true)]
    public async Task UpdateLoanOut_DifferentDates_ReconcilesMarkersAtomically(
        bool recurring,
        bool overlappingLoanOut,
        int newDay,
        bool failTransfer
    )
    {
        var ct = TestContext.Current.CancellationToken;
        var source = await _shiftService.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest() with
            {
                StartAtUtc = AtDay(1, 9),
                EndAtUtc = AtDay(1, 17),
                RecurrenceRule = "FREQ=DAILY;COUNT=6",
                UserIds = [UserA],
            },
            ct
        );
        var otherUser = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(AtDay(2, 9), AtDay(2, 17)) with
            {
                UserIds = [UserB],
            },
            ct
        );
        var originals = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry => entry.ShiftSeriesId == source.Id)
            .ToListAsync(ct);
        var identities = originals.ToDictionary(
            entry => entry.Id,
            entry =>
                (
                    entry.EventId,
                    entry.ShiftSeriesId,
                    entry.Event!.EventSeriesId,
                    entry.Event.StartAtUtc,
                    entry.Event.EndAtUtc,
                    UserRow: Assert.Single(entry.Users).Id
                )
        );
        var unrelated = Assert.Single(originals, entry => entry.Event!.StartAtUtc == AtDay(6, 9));
        unrelated.Event!.IsException = true;
        unrelated.Event.ExceptionReason = "Other";
        await _db.SaveChangesAsync(ct);
        var entryService = CreateLoanOutEntryService();
        var seriesService = CreateLoanOutSeriesService();
        var entryRequest = new LoanOutEntryRequest
        {
            Title = "Away",
            LocationId = 5,
            LoanOutLocationId = 9,
            UserIds = [UserA],
            StartAtUtc = AtDay(2, 7),
            EndAtUtc = AtDay(3, 7),
            TimeZoneId = "America/Vancouver",
        };
        var seriesRequest = new LoanOutSeriesRequest
        {
            Title = entryRequest.Title,
            LocationId = 5,
            LoanOutLocationId = 9,
            UserIds = [UserA],
            StartAtUtc = entryRequest.StartAtUtc,
            EndAtUtc = entryRequest.EndAtUtc,
            TimeZoneId = entryRequest.TimeZoneId,
            RecurrenceRule = "FREQ=DAILY;COUNT=2",
        };
        var id = recurring
            ? (await seriesService.CreateLoanOutSeriesAsync(seriesRequest, ct)).Id
            : (await entryService.CreateLoanOutEntryAsync(entryRequest, ct)).Id;
        var oldDays = recurring ? new[] { 2, 3 } : new[] { 2 };
        _db.ChangeTracker.Clear();
        foreach (
            var entry in await _db
                .ShiftEntries.Include(entry => entry.Event)
                .Where(entry => entry.ShiftSeriesId == source.Id)
                .ToListAsync(ct)
        )
            if (oldDays.Contains(entry.Event!.StartAtUtc.Day))
                Assert.Equal("IsAway", entry.Event.ExceptionReason);

        // Editing remains subject to the existing destination-shift guard.
        foreach (
            var seriesId in await _db
                .ShiftSeries.Where(item => item.EventSeries!.LocationId == 9)
                .Select(item => item.Id)
                .ToListAsync(ct)
        )
            Assert.True(await _shiftService.DeleteShiftSeriesAsync(seriesId, ct));
        foreach (
            var entryId in await _db
                .ShiftEntries.Where(item => item.Event!.LocationId == 9)
                .Select(item => item.Id)
                .ToListAsync(ct)
        )
            Assert.True(await _shiftService.DeleteShiftEntryAsync(entryId, ct));
        if (overlappingLoanOut)
        {
            var remaining = CreateLoanOutEntry(AtDay(2, 7));
            remaining.Event!.EndAtUtc = AtDay(3, 7);
            _db.LoanOutEntries.Add(remaining);
            await _db.SaveChangesAsync(ct);
        }

        if (failTransfer)
        {
            var zones = new TimeZoneService();
            var resolver = new CalendarTimeZoneResolver(
                Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "UTC" }),
                zones
            );
            var availability = SchedulingLoanOutTestServices.CreateAvailability(
                _db,
                zones,
                resolver,
                () => _shiftService
            );
            var markers = new ShiftLoanOutExceptionMarkerService(_db, availability, resolver);
            var failingTransfers = new LoanOutShiftTransferService(
                NullLogger<LoanOutShiftTransferService>.Instance,
                _db,
                CreateLoanOutEventMapper(),
                () => throw new InvalidOperationException("Destination copy failed."),
                () => _linkService,
                availability,
                markers,
                CreateLoanOutRecurrenceExpander()
            );
            var failingIntegration = new SchedulingLoanOutService(availability, markers, failingTransfers);
            entryService = CreateLoanOutEntryService(failingIntegration);
            seriesService = CreateLoanOutSeriesService(failingIntegration);
        }
        async Task UpdateAsync()
        {
            if (recurring)
                Assert.NotNull(
                    await seriesService.UpdateLoanOutSeriesAsync(
                        id,
                        seriesRequest with
                        {
                            StartAtUtc = AtDay(newDay, 7),
                            EndAtUtc = AtDay(newDay + 1, 7),
                        },
                        ct
                    )
                );
            else
                Assert.NotNull(
                    await entryService.UpdateLoanOutEntryAsync(
                        id,
                        entryRequest with
                        {
                            StartAtUtc = AtDay(newDay, 7),
                            EndAtUtc = AtDay(newDay + 1, 7),
                        },
                        ct
                    )
                );
        }
        if (failTransfer)
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(UpdateAsync);
            Assert.Equal("Destination copy failed.", error.Message);
        }
        else
            await UpdateAsync();
        _db.ChangeTracker.Clear();

        var newDays = failTransfer ? oldDays : Enumerable.Range(newDay, recurring ? 2 : 1).ToArray();
        var retained = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry => entry.ShiftSeriesId == source.Id)
            .OrderBy(entry => entry.Event!.StartAtUtc)
            .ToListAsync(ct);
        Assert.Equal(identities.Keys.Order(), retained.Select(entry => entry.Id).Order());
        foreach (var entry in retained)
        {
            Assert.Equal(
                identities[entry.Id],
                (
                    entry.EventId,
                    entry.ShiftSeriesId,
                    entry.Event!.EventSeriesId,
                    entry.Event.StartAtUtc,
                    entry.Event.EndAtUtc,
                    Assert.Single(entry.Users).Id
                )
            );
            Assert.Equal(UserA, Assert.Single(entry.Users).UserId);
            var day = entry.Event.StartAtUtc.Day;
            var reason =
                day == 6 ? "Other"
                : newDays.Contains(day) || overlappingLoanOut && day == 2 ? "IsAway"
                : null;
            Assert.Equal(reason, entry.Event.ExceptionReason);
            Assert.Equal(reason is not null, entry.Event.IsException);
        }
        var untouched = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .SingleAsync(entry => entry.Id == otherUser.Id, ct);
        Assert.False(untouched.Event!.IsException);
        Assert.Equal(UserB, Assert.Single(untouched.Users).UserId);
        var copied = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .Where(entry => entry.Event!.LocationId == 9)
            .ToListAsync(ct);
        Assert.Equal(
            failTransfer ? [] : newDays.Select(day => AtDay(day, 9)),
            copied.Select(entry => entry.Event!.StartAtUtc).Order()
        );
        var expectedLoanOutStart = AtDay(failTransfer ? 2 : newDay, 7);
        if (recurring)
            Assert.Equal(expectedLoanOutStart, (await seriesService.GetLoanOutSeriesByIdAsync(id, ct))!.StartAtUtc);
        else
            Assert.Equal(expectedLoanOutStart, (await entryService.GetLoanOutEntryByIdAsync(id, ct))!.StartAtUtc);
        Assert.All(copied, entry => Assert.Equal(UserA, Assert.Single(entry.Users).UserId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateLoanOut_WhenDestinationShiftBlocksEdit_PreservesOriginalMarkersAndDates(bool recurring)
    {
        var ct = TestContext.Current.CancellationToken;
        var source = await _shiftService.CreateShiftEntryAsync(CreateShiftEntryRequest(AtDay(2, 9), AtDay(2, 17)), ct);
        var entryRequest = new LoanOutEntryRequest
        {
            Title = "Away",
            LocationId = 5,
            LoanOutLocationId = 9,
            UserIds = [UserA],
            StartAtUtc = AtDay(2, 7),
            EndAtUtc = AtDay(3, 7),
            TimeZoneId = "America/Vancouver",
        };
        var seriesRequest = new LoanOutSeriesRequest
        {
            Title = entryRequest.Title,
            LocationId = 5,
            LoanOutLocationId = 9,
            UserIds = [UserA],
            StartAtUtc = entryRequest.StartAtUtc,
            EndAtUtc = entryRequest.EndAtUtc,
            TimeZoneId = entryRequest.TimeZoneId,
            RecurrenceRule = "FREQ=DAILY;COUNT=2",
        };
        var entries = CreateLoanOutEntryService();
        var series = CreateLoanOutSeriesService();
        var id = recurring
            ? (await series.CreateLoanOutSeriesAsync(seriesRequest, ct)).Id
            : (await entries.CreateLoanOutEntryAsync(entryRequest, ct)).Id;

        if (recurring)
            await Assert.ThrowsAsync<ConflictValidationException>(() =>
                series.UpdateLoanOutSeriesAsync(
                    id,
                    seriesRequest with
                    {
                        StartAtUtc = AtDay(4, 7),
                        EndAtUtc = AtDay(5, 7),
                    },
                    ct
                )
            );
        else
            await Assert.ThrowsAsync<ConflictValidationException>(() =>
                entries.UpdateLoanOutEntryAsync(
                    id,
                    entryRequest with
                    {
                        StartAtUtc = AtDay(4, 7),
                        EndAtUtc = AtDay(5, 7),
                    },
                    ct
                )
            );
        _db.ChangeTracker.Clear();

        var retained = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .SingleAsync(entry => entry.Id == source.Id, ct);
        Assert.True(retained.Event!.IsException);
        Assert.Equal("IsAway", retained.Event.ExceptionReason);
        if (recurring)
            Assert.Equal(AtDay(2, 7), (await series.GetLoanOutSeriesByIdAsync(id, ct))!.StartAtUtc);
        else
            Assert.Equal(AtDay(2, 7), (await entries.GetLoanOutEntryByIdAsync(id, ct))!.StartAtUtc);
        Assert.Equal(1, await _db.ShiftEntries.CountAsync(entry => entry.Event!.LocationId == 9, ct));
    }

    private LoanOutEntryService CreateLoanOutEntryService(ISchedulingLoanOutService? integration = null) =>
        new(
            NullLogger<LoanOutEntryService>.Instance,
            _db,
            new LoanOutEventMapper(new CalendarEventMapper()),
            Entry<LoanOutEntryRequest, LoanOutEntry>(new LoanOutEventFactory()),
            integration ?? _schedulingLoanOutService
        );

    private LoanOutSeriesService CreateLoanOutSeriesService(ISchedulingLoanOutService? integration = null) =>
        new(
            NullLogger<LoanOutSeriesService>.Instance,
            _db,
            new LoanOutEventMapper(new CalendarEventMapper()),
            new CalendarEventSeriesService<LoanOutSeriesRequest, LoanOutSeries>(
                new LoanOutEventFactory(),
                new CalendarEventMapper(),
                new CalendarLifecycleService()
            ),
            _materializationService,
            new LoanOutSeriesMaterializationHandler(_db),
            integration ?? _schedulingLoanOutService
        );

    [Fact]
    public async Task DeleteLoanOut_WhenDestinationShiftBlocksDeletion_PreservesAwayFlags()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = await _shiftService.CreateShiftEntryAsync(CreateShiftEntryRequest(AtDay(2, 9), AtDay(2, 17)), ct);
        await SeedHomeLoanOutValidationAsync(AtDay(2, 7), AtDay(3, 7));
        var loanOut = await _db
            .LoanOutEntries.Include(entry => entry.Event)
            .Include(entry => entry.Users)
            .SingleAsync(ct);
        Assert.True(await _schedulingLoanOutService.TransferLoanedOutUserShift(loanOut, cancellationToken: ct));
        var service = new LoanOutEntryService(
            NullLogger<LoanOutEntryService>.Instance,
            _db,
            new LoanOutEventMapper(new CalendarEventMapper()),
            Entry<LoanOutEntryRequest, LoanOutEntry>(new LoanOutEventFactory()),
            _schedulingLoanOutService
        );

        await Assert.ThrowsAsync<ConflictValidationException>(() => service.DeleteLoanOutEntryAsync(loanOut.Id, ct));

        _db.ChangeTracker.Clear();
        var retained = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .SingleAsync(entry => entry.Id == source.Id, ct);
        Assert.True(retained.Event!.IsException);
        Assert.Equal("IsAway", retained.Event.ExceptionReason);
        Assert.True(await _db.LoanOutEntries.AnyAsync(entry => entry.Id == loanOut.Id, ct));
    }

    private async Task SeedHomeLoanOutValidationAsync(DateTimeOffset start, DateTimeOffset end, bool cancelled = false)
    {
        var ct = TestContext.Current.CancellationToken;
        var user = await _db.Users.SingleAsync(user => user.Id == UserA, ct);
        user.HomeLocationId = 5;
        var loanOut = CreateLoanOutEntry(start);
        loanOut.Event!.EndAtUtc = end;
        loanOut.Event.AllDay = true;
        loanOut.Event.TimeZoneId = "America/Vancouver";
        if (cancelled)
            loanOut.Event.StatusTypeCode = CalendarEventStatusTypeCodes.Cancelled;
        _db.LoanOutEntries.Add(loanOut);
        await _db.SaveChangesAsync(ct);
    }

    private static LoanOutEntry CreateLoanOutEntry(DateTimeOffset startAtUtc) =>
        new()
        {
            LoanOutLocationId = 9,
            Event = new Event
            {
                Title = "Loan out",
                StartAtUtc = startAtUtc,
                EndAtUtc = startAtUtc.AddHours(1),
                TimeZoneId = "UTC",
                LocationId = 5,
                SourceModule = LoanOutConstants.SourceModule,
                EventTypeCode = LoanOutConstants.LoanOutEventTypeCode,
                StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
            },
            Users = [new LoanOutEntryUser { UserId = UserA }],
        };

    [Fact]
    public async Task CreateAssignmentSeriesAsync_WithShiftSeriesLinks_CreatesLinksAtomically()
    {
        var shiftSeries = await _shiftService.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest(),
            TestContext.Current.CancellationToken
        );

        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest() with
            {
                ShiftSeriesLinks =
                [
                    new ShiftSeriesLinkRequest { ShiftSeriesId = shiftSeries.Id, AssignedUserIds = [UserA] },
                ],
            },
            TestContext.Current.CancellationToken
        );

        var link = Assert.Single(assignmentSeries.ShiftSeriesLinks);
        Assert.Equal(shiftSeries.Id, link.ShiftSeriesId);
        Assert.Equal([UserA], link.AssignedUserIds);
    }

    [Fact]
    public async Task CreateShiftSeriesAsync_WithAssignmentSeriesLinks_CreatesLinksAtomically()
    {
        var assignmentSeries = await _assignmentService.CreateAssignmentSeriesAsync(
            CreateAssignmentSeriesRequest() with
            {
                StartAtUtc = At(6),
                EndAtUtc = At(7),
            },
            TestContext.Current.CancellationToken
        );

        var shiftSeries = await _shiftService.CreateShiftSeriesAsync(
            CreateShiftSeriesRequest() with
            {
                AssignmentSeriesLinks =
                [
                    new AssignmentSeriesLinkRequest
                    {
                        AssignmentSeriesId = assignmentSeries.Id,
                        AssignedUserIds = [UserA],
                    },
                ],
            },
            TestContext.Current.CancellationToken
        );

        var link = Assert.Single(
            await _db.ShiftAssignmentSeriesLinks.ToListAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(shiftSeries.Id, link.ShiftSeriesId);
        Assert.Equal(assignmentSeries.Id, link.AssignmentSeriesId);
    }

    [Fact]
    public async Task UpdateAssignmentEntryAsync_WhenLinkedSeriesOccurrenceTitleChanges_PreservesLinkAndUsers()
    {
        var (shiftSeries, assignmentSeries) = await CreateLinkedSeriesAsync();
        var assignmentEntryId = Assert.Single(assignmentSeries.AssignmentEntryIds);
        var shiftEntryId = Assert.Single(shiftSeries.ShiftEntryIds);

        _db.ChangeTracker.Clear();

        var updated = await _assignmentService.UpdateAssignmentEntryAsync(
            assignmentEntryId,
            CreateAssignmentEntryUpdateRequest() with
            {
                Title = "Updated assignment occurrence",
            },
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(updated);
        Assert.Equal("Updated assignment occurrence", updated.Title);
        Assert.Equal([shiftEntryId], updated.LinkedShiftEntryIds);
        Assert.Equal([UserA], updated.AssignedUserIds);
        var storedLink = Assert.Single(
            await _db
                .ShiftAssignmentEntries.Include(link => link.Users)
                .Where(link => link.AssignmentEntryId == assignmentEntryId)
                .ToListAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(shiftEntryId, storedLink.ShiftEntryId);
        Assert.Equal([UserA], storedLink.Users.Select(user => user.UserId));
    }

    [Fact]
    public async Task CreateShiftEntryAsync_WithAssignmentEntryLinks_CreatesLinksAtomically()
    {
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(),
            TestContext.Current.CancellationToken
        );

        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(17)) with
            {
                AssignmentEntryLinks =
                [
                    new AssignmentEntryLinkRequest { AssignmentEntryId = assignment.Id, AssignedUserIds = [UserA] },
                ],
            },
            TestContext.Current.CancellationToken
        );

        var link = Assert.Single(await _db.ShiftAssignmentEntries.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(shift.Id, link.ShiftEntryId);
        Assert.Equal(assignment.Id, link.AssignmentEntryId);
    }

    [Fact]
    public async Task CreateAssignmentEntryAsync_WithShiftEntryLinks_CreatesLinksAtomically()
    {
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(17)),
            TestContext.Current.CancellationToken
        );

        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest() with
            {
                ShiftEntryLinks = [new ShiftEntryLinkRequest { ShiftEntryId = shift.Id, AssignedUserIds = [UserA] }],
            },
            TestContext.Current.CancellationToken
        );

        var link = Assert.Single(await _db.ShiftAssignmentEntries.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(shift.Id, link.ShiftEntryId);
        Assert.Equal(assignment.Id, link.AssignmentEntryId);
    }

    [Fact]
    public async Task UpdateAssignmentEntryAsync_WithNullShiftEntryLinks_PreservesExistingRelationships()
    {
        var (shift, assignment, _) = await CreateLinkedEntriesAsync();

        var updated = await _assignmentService.UpdateAssignmentEntryAsync(
            assignment.Id,
            CreateAssignmentEntryUpdateRequest() with
            {
                Title = "Updated assignment",
            },
            TestContext.Current.CancellationToken
        );

        var link = Assert.Single(
            await _db
                .ShiftAssignmentEntries.Include(existingLink => existingLink.Users)
                .ToListAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal("Updated assignment", updated!.Title);
        Assert.Equal(shift.Id, link.ShiftEntryId);
        Assert.Equal([UserA], link.Users.Select(user => user.UserId));
    }

    [Fact]
    public async Task UpdateAssignmentEntryAsync_WithEmptyShiftEntryLinks_RemovesAllRelationships()
    {
        var (_, assignment, _) = await CreateLinkedEntriesAsync();

        await _assignmentService.UpdateAssignmentEntryAsync(
            assignment.Id,
            CreateAssignmentEntryUpdateRequest() with
            {
                ShiftEntryLinks = [],
            },
            TestContext.Current.CancellationToken
        );

        Assert.Empty(await _db.ShiftAssignmentEntries.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAssignmentEntryAsync_WithPopulatedShiftEntryLinks_ReconcilesLinksAndUsers()
    {
        var retainedShift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(17)) with
            {
                UserIds = [UserA, UserB],
            },
            TestContext.Current.CancellationToken
        );
        var removedShift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(8), At(16)) with
            {
                UserIds = [UserC],
            },
            TestContext.Current.CancellationToken
        );
        var addedShift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(7), At(15)) with
            {
                UserIds = [UserD],
            },
            TestContext.Current.CancellationToken
        );
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest() with
            {
                ShiftEntryLinks =
                [
                    new ShiftEntryLinkRequest { ShiftEntryId = retainedShift.Id, AssignedUserIds = [UserA] },
                    new ShiftEntryLinkRequest { ShiftEntryId = removedShift.Id, AssignedUserIds = [UserC] },
                ],
            },
            TestContext.Current.CancellationToken
        );

        await _assignmentService.UpdateAssignmentEntryAsync(
            assignment.Id,
            CreateAssignmentEntryUpdateRequest() with
            {
                ShiftEntryLinks =
                [
                    new ShiftEntryLinkRequest { ShiftEntryId = retainedShift.Id, AssignedUserIds = [UserB] },
                    new ShiftEntryLinkRequest { ShiftEntryId = addedShift.Id, AssignedUserIds = [UserD] },
                ],
            },
            TestContext.Current.CancellationToken
        );

        var links = await _db
            .ShiftAssignmentEntries.Include(link => link.Users)
            .OrderBy(link => link.ShiftEntryId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, links.Count);
        Assert.Equal(
            [UserB],
            Assert.Single(links, link => link.ShiftEntryId == retainedShift.Id).Users.Select(user => user.UserId)
        );
        Assert.Equal(
            [UserD],
            Assert.Single(links, link => link.ShiftEntryId == addedShift.Id).Users.Select(user => user.UserId)
        );
        Assert.DoesNotContain(links, link => link.ShiftEntryId == removedShift.Id);
    }

    [Fact]
    public async Task UpdateAssignmentEntryAsync_WhenLaterRelationshipFails_RollsBackFieldsAndEarlierRelationships()
    {
        var existingShift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(17)),
            TestContext.Current.CancellationToken
        );
        var replacementShift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(8), At(16)) with
            {
                UserIds = [UserB],
            },
            TestContext.Current.CancellationToken
        );
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest() with
            {
                ShiftEntryLinks =
                [
                    new ShiftEntryLinkRequest { ShiftEntryId = existingShift.Id, AssignedUserIds = [UserA] },
                ],
            },
            TestContext.Current.CancellationToken
        );

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _assignmentService.UpdateAssignmentEntryAsync(
                assignment.Id,
                CreateAssignmentEntryUpdateRequest() with
                {
                    Title = "Should roll back",
                    ShiftEntryLinks =
                    [
                        new ShiftEntryLinkRequest { ShiftEntryId = replacementShift.Id, AssignedUserIds = [UserB] },
                        new ShiftEntryLinkRequest { ShiftEntryId = 999, AssignedUserIds = [UserA] },
                    ],
                },
                TestContext.Current.CancellationToken
            )
        );

        _db.ChangeTracker.Clear();
        var storedAssignment = await _db
            .AssignmentEntries.Include(entry => entry.Event)
            .SingleAsync(entry => entry.Id == assignment.Id, TestContext.Current.CancellationToken);
        var storedLink = Assert.Single(
            await _db.ShiftAssignmentEntries.ToListAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal("Assignment", storedAssignment.Event!.Title);
        Assert.Equal(existingShift.Id, storedLink.ShiftEntryId);
    }

    [Fact]
    public async Task UpdateAssignmentEntryAsync_WithNullShiftEntryLinks_PreservesZeroUserRelationship()
    {
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(17)),
            TestContext.Current.CancellationToken
        );
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(),
            TestContext.Current.CancellationToken
        );
        var zeroUserLink = new ShiftAssignmentEntry { ShiftEntryId = shift.Id, AssignmentEntryId = assignment.Id };
        _db.ShiftAssignmentEntries.Add(zeroUserLink);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _assignmentService.UpdateAssignmentEntryAsync(
            assignment.Id,
            CreateAssignmentEntryUpdateRequest() with
            {
                Notes = "Unrelated change",
            },
            TestContext.Current.CancellationToken
        );

        var storedLink = Assert.Single(
            await _db.ShiftAssignmentEntries.ToListAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(zeroUserLink.Id, storedLink.Id);
        Assert.Empty(storedLink.Users);
    }

    [Fact]
    public async Task UpdateAssignmentEntryAsync_WithDifferentDesiredLink_ReplacesExistingLink()
    {
        var firstShift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(17)),
            TestContext.Current.CancellationToken
        );
        var secondShift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(8), At(16)) with
            {
                UserIds = [UserB],
            },
            TestContext.Current.CancellationToken
        );
        var assignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest() with
            {
                ShiftEntryLinks =
                [
                    new ShiftEntryLinkRequest { ShiftEntryId = firstShift.Id, AssignedUserIds = [UserA] },
                ],
            },
            TestContext.Current.CancellationToken
        );

        await _assignmentService.UpdateAssignmentEntryAsync(
            assignment.Id,
            CreateAssignmentEntryUpdateRequest() with
            {
                ShiftEntryLinks =
                [
                    new ShiftEntryLinkRequest { ShiftEntryId = secondShift.Id, AssignedUserIds = [UserB] },
                ],
            },
            TestContext.Current.CancellationToken
        );

        var link = Assert.Single(await _db.ShiftAssignmentEntries.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(secondShift.Id, link.ShiftEntryId);
        Assert.Equal(assignment.Id, link.AssignmentEntryId);
    }

    [Fact]
    public async Task CreateShiftEntryAsync_WhenDesiredLinkIsInvalid_RollsBackShift()
    {
        var shiftCount = await _db.ShiftEntries.CountAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _shiftService.CreateShiftEntryAsync(
                CreateShiftEntryRequest(At(9), At(17)) with
                {
                    AssignmentEntryLinks =
                    [
                        new AssignmentEntryLinkRequest { AssignmentEntryId = 999, AssignedUserIds = [UserA] },
                    ],
                },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(shiftCount, await _db.ShiftEntries.CountAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await _db.ShiftAssignmentEntries.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateShiftSeriesAsync_WhenDesiredLinkIsInvalid_RollsBackSeriesAndEntries()
    {
        var seriesCount = await _db.ShiftSeries.CountAsync(TestContext.Current.CancellationToken);
        var entryCount = await _db.ShiftEntries.CountAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _shiftService.CreateShiftSeriesAsync(
                CreateShiftSeriesRequest() with
                {
                    AssignmentSeriesLinks =
                    [
                        new AssignmentSeriesLinkRequest { AssignmentSeriesId = 999, AssignedUserIds = [UserA] },
                    ],
                },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(seriesCount, await _db.ShiftSeries.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(entryCount, await _db.ShiftEntries.CountAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await _db.ShiftAssignmentSeriesLinks.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateShiftEntryAsync_WhenDesiredLinkIsInvalid_RollsBackShiftMutation()
    {
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(17)),
            TestContext.Current.CancellationToken
        );

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _shiftService.UpdateShiftEntryAsync(
                shift.Id,
                CreateShiftEntryRequest(At(9), At(17)) with
                {
                    Title = "Changed title",
                    AssignmentEntryLinks =
                    [
                        new AssignmentEntryLinkRequest { AssignmentEntryId = 999, AssignedUserIds = [UserA] },
                    ],
                },
                TestContext.Current.CancellationToken
            )
        );

        _db.ChangeTracker.Clear();
        var storedEvent = await _db.Events.SingleAsync(
            eventEntity => eventEntity.Id == shift.EventId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal("Shift", storedEvent.Title);
        Assert.Empty(await _db.ShiftAssignmentEntries.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateShiftEntryAsync_WithNullAssignmentEntryLinks_PreservesExistingRelationships()
    {
        var (shift, assignment, _) = await CreateLinkedEntriesAsync();

        var updated = await _shiftService.UpdateShiftEntryAsync(
            shift.Id,
            CreateShiftEntryRequest(At(9), At(17)) with
            {
                Title = "Updated shift",
            },
            TestContext.Current.CancellationToken
        );

        var link = Assert.Single(
            await _db
                .ShiftAssignmentEntries.Include(existingLink => existingLink.Users)
                .ToListAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal("Updated shift", updated!.Title);
        Assert.Equal(assignment.Id, link.AssignmentEntryId);
        Assert.Equal([UserA], link.Users.Select(user => user.UserId));
    }

    [Fact]
    public async Task UpdateShiftEntryAsync_WithEmptyAssignmentEntryLinks_RemovesAllRelationships()
    {
        var (shift, _, _) = await CreateLinkedEntriesAsync();

        await _shiftService.UpdateShiftEntryAsync(
            shift.Id,
            CreateShiftEntryRequest(At(9), At(17)) with
            {
                AssignmentEntryLinks = [],
            },
            TestContext.Current.CancellationToken
        );

        Assert.Empty(await _db.ShiftAssignmentEntries.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateShiftEntryAsync_WithPopulatedAssignmentEntryLinks_ReconcilesLinksAndUsers()
    {
        var retainedAssignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(),
            TestContext.Current.CancellationToken
        );
        var removedAssignment = await CreateAssignmentEntryForNewDefinitionAsync("Removed definition");
        var addedAssignment = await CreateAssignmentEntryForNewDefinitionAsync("Added definition");
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(17)) with
            {
                UserIds = [UserA, UserB],
                AssignmentEntryLinks =
                [
                    new AssignmentEntryLinkRequest
                    {
                        AssignmentEntryId = retainedAssignment.Id,
                        AssignedUserIds = [UserA],
                    },
                    new AssignmentEntryLinkRequest
                    {
                        AssignmentEntryId = removedAssignment.Id,
                        AssignedUserIds = [UserB],
                    },
                ],
            },
            TestContext.Current.CancellationToken
        );

        await _shiftService.UpdateShiftEntryAsync(
            shift.Id,
            CreateShiftEntryRequest(At(9), At(17)) with
            {
                UserIds = [UserA, UserB],
                AssignmentEntryLinks =
                [
                    new AssignmentEntryLinkRequest
                    {
                        AssignmentEntryId = retainedAssignment.Id,
                        AssignedUserIds = [UserB],
                    },
                    new AssignmentEntryLinkRequest
                    {
                        AssignmentEntryId = addedAssignment.Id,
                        AssignedUserIds = [UserA],
                    },
                ],
            },
            TestContext.Current.CancellationToken
        );

        var links = await _db
            .ShiftAssignmentEntries.Include(link => link.Users)
            .OrderBy(link => link.AssignmentEntryId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, links.Count);
        Assert.Equal(
            [UserB],
            Assert
                .Single(links, link => link.AssignmentEntryId == retainedAssignment.Id)
                .Users.Select(user => user.UserId)
        );
        Assert.Equal(
            [UserA],
            Assert.Single(links, link => link.AssignmentEntryId == addedAssignment.Id).Users.Select(user => user.UserId)
        );
        Assert.DoesNotContain(links, link => link.AssignmentEntryId == removedAssignment.Id);
    }

    [Fact]
    public async Task UpdateShiftEntryAsync_WhenLaterRelationshipFails_RollsBackFieldsAndEarlierRelationships()
    {
        var existingAssignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(),
            TestContext.Current.CancellationToken
        );
        var replacementAssignment = await CreateAssignmentEntryForNewDefinitionAsync("Replacement definition");
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(17)) with
            {
                UserIds = [UserA, UserB],
                AssignmentEntryLinks =
                [
                    new AssignmentEntryLinkRequest
                    {
                        AssignmentEntryId = existingAssignment.Id,
                        AssignedUserIds = [UserA],
                    },
                ],
            },
            TestContext.Current.CancellationToken
        );

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _shiftService.UpdateShiftEntryAsync(
                shift.Id,
                CreateShiftEntryRequest(At(9), At(17)) with
                {
                    Title = "Should roll back",
                    UserIds = [UserA, UserB],
                    AssignmentEntryLinks =
                    [
                        new AssignmentEntryLinkRequest
                        {
                            AssignmentEntryId = replacementAssignment.Id,
                            AssignedUserIds = [UserB],
                        },
                        new AssignmentEntryLinkRequest { AssignmentEntryId = 999, AssignedUserIds = [UserA] },
                    ],
                },
                TestContext.Current.CancellationToken
            )
        );

        _db.ChangeTracker.Clear();
        var storedShift = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .SingleAsync(entry => entry.Id == shift.Id, TestContext.Current.CancellationToken);
        var storedLink = Assert.Single(
            await _db.ShiftAssignmentEntries.ToListAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal("Shift", storedShift.Event!.Title);
        Assert.Equal(existingAssignment.Id, storedLink.AssignmentEntryId);
    }

    [Fact]
    public async Task LinkedEntryUpdates_WhenTheyInvalidateLinks_ShiftRejectsAndAssignmentUnlinks()
    {
        var (shift, assignment, _) = await CreateLinkedEntriesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _shiftService.UpdateShiftEntryAsync(
                shift.Id,
                CreateShiftEntryRequest(At(10), At(12)) with
                {
                    UserIds = [UserB],
                    AssignmentEntryLinks =
                    [
                        new AssignmentEntryLinkRequest { AssignmentEntryId = assignment.Id, AssignedUserIds = [UserA] },
                    ],
                },
                TestContext.Current.CancellationToken
            )
        );
        var updatedAssignment = await _assignmentService.UpdateAssignmentEntryAsync(
            assignment.Id,
            CreateAssignmentEntryUpdateRequest(AtDay(2, 20), AtDay(2, 22)) with
            {
                ShiftEntryLinks = [new ShiftEntryLinkRequest { ShiftEntryId = shift.Id, AssignedUserIds = [UserA] }],
            },
            TestContext.Current.CancellationToken
        );

        var storedShift = await _db
            .ShiftEntries.Include(x => x.Users)
            .SingleAsync(x => x.Id == shift.Id, TestContext.Current.CancellationToken);
        var storedAssignment = await _db
            .AssignmentEntries.Include(x => x.Event)
            .SingleAsync(x => x.Id == assignment.Id, TestContext.Current.CancellationToken);
        Assert.Equal([UserA], storedShift.Users.Select(x => x.UserId));
        Assert.Equal(AtDay(2, 20), updatedAssignment!.StartAtUtc);
        Assert.Equal(AtDay(2, 20), storedAssignment.Event!.StartAtUtc);
        Assert.Empty(await _db.ShiftAssignmentEntries.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LinkedAssignmentUpdate_WhenMovedOutsideShiftTimeOnSameDate_RemainsValid()
    {
        var (shift, assignment, _) = await CreateLinkedEntriesAsync();

        var updated = await _assignmentService.UpdateAssignmentEntryAsync(
            assignment.Id,
            CreateAssignmentEntryUpdateRequest(At(20), At(22)) with
            {
                ShiftEntryLinks = [new ShiftEntryLinkRequest { ShiftEntryId = shift.Id, AssignedUserIds = [UserA] }],
            },
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(updated);
        Assert.Equal(At(20), updated.StartAtUtc);
        Assert.True(
            await _db.ShiftAssignmentEntries.AnyAsync(
                link => link.ShiftEntryId == shift.Id && link.AssignmentEntryId == assignment.Id,
                TestContext.Current.CancellationToken
            )
        );
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

    private async Task<AssignmentEntryResponse> CreateAssignmentEntryForNewDefinitionAsync(string name)
    {
        var definition = await _definitionService.CreateAssignmentDefinitionAsync(
            CreateDefinitionRequest() with
            {
                Name = name,
            },
            TestContext.Current.CancellationToken
        );
        return await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest() with
            {
                AssignmentDefinitionId = definition.Id,
            },
            TestContext.Current.CancellationToken
        );
    }

    private async Task<(
        ShiftEntryResponse Shift,
        AssignmentEntryResponse FirstAssignment,
        AssignmentEntryResponse SecondAssignment
    )> CreateDraftConflictAsync()
    {
        var shift = await _shiftService.CreateShiftEntryAsync(
            CreateShiftEntryRequest(At(9), At(12)),
            TestContext.Current.CancellationToken
        );
        var firstAssignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(10), At(12)),
            TestContext.Current.CancellationToken
        );
        var secondAssignment = await _assignmentService.CreateAssignmentEntryAsync(
            CreateAssignmentEntryRequest(At(10), At(12)) with
            {
                AssignmentDefinitionId = 101,
            },
            TestContext.Current.CancellationToken
        );
        await _linkService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = shift.Id,
                AssignmentEntryId = firstAssignment.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );
        await _linkService.LinkShiftEntryAsync(
            new ShiftAssignmentEntryRequest
            {
                ShiftEntryId = shift.Id,
                AssignmentEntryId = secondAssignment.Id,
                UserIds = [UserA],
            },
            TestContext.Current.CancellationToken
        );
        return (shift, firstAssignment, secondAssignment);
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
            includeLeave: false,
            includeLoanOut: false,
            TestContext.Current.CancellationToken
        );

    private SchedulePublishService CreateSchedulePublishService() =>
        new(
            _db,
            new CalendarTimeZoneResolver(
                Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "America/Vancouver" }),
                new TimeZoneService()
            ),
            new TimeZoneService(),
            _shiftService,
            _assignmentService,
            _conflictService,
            NullLogger<SchedulePublishService>.Instance
        );

    private static SchedulePublishRequest CreateSchedulePublishRequest() =>
        new(5, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1));

    private async Task SeedBaseDataAsync()
    {
        _db.EventTypes.AddRange(
            CreateEventType(SchedulingConstants.ShiftEventTypeCode),
            CreateEventType(SchedulingConstants.AssignmentEventTypeCode),
            CreateEventType(LoanOutConstants.LoanOutEventTypeCode)
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
        _db.Users.AddRange(
            CreateUser(UserA, "A"),
            CreateUser(UserB, "B"),
            CreateUser(UserC, "C"),
            CreateUser(UserD, "D")
        );
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
        _db.AssignmentDefinitions.AddRange(
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
            },
            new AssignmentDefinition
            {
                Id = 101,
                LocationId = 5,
                Name = "Alternate definition",
                NormalizedName = "ALTERNATE DEFINITION",
                CategoryId = 10,
                SubCategoryId = 11,
                Color = "alternate-color",
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

    private static ShiftEntryUpdateRequest CreateShiftEntryRequest(
        DateTimeOffset startAtUtc,
        DateTimeOffset endAtUtc
    ) =>
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

    private static CalendarEventEntryService<TRequest, TEntity> Entry<TRequest, TEntity>(
        ICalendarEventEntryFactory<TRequest, TEntity> factory
    )
        where TRequest : CalendarEventEntryRequest
        where TEntity : class =>
        new(
            NullLogger<CalendarEventEntryService<TRequest, TEntity>>.Instance,
            factory,
            new CalendarLifecycleService(),
            new CalendarEventMapper()
        );

    private static ShiftService CreateShift(
        UnifiedDbContext db,
        IEventSeriesMaterializationService materializer,
        IRecurrenceExpander expander,
        IShiftAssignmentService links,
        ITimeZoneService zones,
        TimeProvider time,
        ICalendarConflictService conflicts
    )
    {
        var factory = new ShiftEventFactory();
        var lifecycle = new CalendarLifecycleService();
        var shiftConflicts = new ShiftConflictService(
            db,
            zones,
            SchedulingLoanOutTestServices.CreateAvailability(db, zones)
        );
        var loanOutProvider = new LoanOutCalendarDataProvider(db, new LoanOutCalendarMapper(), zones);
        var series = new ShiftSeriesService(
            NullLogger<ShiftSeriesService>.Instance,
            db,
            new CalendarEventSeriesService<ShiftSeriesRequest, ShiftSeries>(
                factory,
                new CalendarEventMapper(),
                lifecycle
            ),
            materializer,
            expander,
            new ShiftSeriesMaterializationHandler(db),
            links,
            lifecycle,
            zones,
            time,
            conflicts,
            shiftConflicts,
            SchedulingLoanOutTestServices.CreateMarker(db, zones),
            schedulingCalendarDataProvider: loanOutProvider
        );
        var entries = new ShiftEntryService(
            NullLogger<ShiftEntryService>.Instance,
            db,
            Entry<ShiftEntryRequest, ShiftEntry>(factory),
            series,
            links,
            lifecycle,
            zones,
            time,
            conflicts,
            shiftConflicts,
            schedulingCalendarDataProvider: loanOutProvider
        );
        return new ShiftService(entries, series);
    }

    private static AssignmentService CreateAssignment(
        UnifiedDbContext db,
        IEventSeriesMaterializationService materializer,
        IShiftAssignmentService links,
        ITimeZoneService zones,
        TimeProvider time,
        ICalendarConflictService conflicts
    )
    {
        var factory = new AssignmentEventFactory();
        var lifecycle = new CalendarLifecycleService();
        return new AssignmentService(
            new AssignmentEntryService(
                NullLogger<AssignmentEntryService>.Instance,
                db,
                Entry<AssignmentEntryRequest, AssignmentEntry>(factory),
                links,
                lifecycle,
                zones,
                time,
                conflicts
            ),
            new AssignmentSeriesService(
                NullLogger<AssignmentSeriesService>.Instance,
                db,
                new CalendarEventSeriesService<AssignmentSeriesRequest, AssignmentSeries>(
                    factory,
                    new CalendarEventMapper(),
                    lifecycle
                ),
                materializer,
                new AssignmentSeriesMaterializationHandler(db),
                links,
                lifecycle,
                zones,
                time,
                conflicts
            )
        );
    }
}
