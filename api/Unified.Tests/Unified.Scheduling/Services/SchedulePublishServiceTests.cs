using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Authorization;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Holidays;
using Unified.Calendar.Mappings;
using Unified.Calendar.Models;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Calendar.Conflicts;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Leave;
using Unified.Db.Models.LoanOut;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.Scheduling;
using Unified.Db.Models.Stats;
using Unified.Db.Models.Training;
using Unified.Db.Models.UserManagement;
using Unified.Leave.Conflicts;
using Unified.Leave.Constants;
using Unified.Leave.Mappings;
using Unified.Leave.Services;
using Unified.LoanOut.Constants;
using Unified.LoanOut.Mappers;
using Unified.LoanOut.Services;
using Unified.Scheduling;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;
using Unified.Tests.TestHelpers;
using Unified.Training.Services;

namespace Unified.Tests.Scheduling.Services;

public sealed class SchedulePublishServiceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset ShiftStart = new(2026, 10, 6, 16, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ShiftEnd = ShiftStart.AddHours(8);
    private static readonly Guid ResourceId = new("11111111-1111-1111-1111-111111111111");

    private SqliteConnection _connection = null!;
    private UnifiedDbContext _db = null!;
    private readonly List<string> _databaseCommands = [];

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseSqlite(_connection)
            .LogTo(_databaseCommands.Add, [RelationalEventId.CommandExecuted])
            .Options;
        _db = new SqliteTestUnifiedDbContext(options);
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await SeedBaseDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Theory]
    [InlineData(5, 6, 6, true)]
    [InlineData(5, 7, 7, false)]
    [InlineData(5, 6, 8, true)]
    [InlineData(6, 6, 6, true)]
    [InlineData(6, 7, 7, true)]
    [InlineData(6, 6, 8, true)]
    public async Task PreviewAsync_IncludesCurrentHomeOrMatchingLocationHistory(
        int locationId,
        int startDay,
        int endDay,
        bool expectedPresent
    )
    {
        _db.Users.Add(
            new User
            {
                Id = ResourceId,
                IdirName = "moved-employee",
                FirstName = "Moved",
                LastName = "Employee",
                IsEnabled = true,
                HomeLocationId = 6,
                UserLocations =
                [
                    new UserLocation
                    {
                        LocationId = 5,
                        EffectiveFrom = null,
                        EffectiveUntil = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero),
                    },
                ],
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _db.ChangeTracker.Clear();
        _databaseCommands.Clear();
        var preview = await CreateService([])
            .PreviewAsync(
                new SchedulePublishRequest(
                    locationId,
                    new DateOnly(2026, 10, startDay),
                    new DateOnly(2026, 10, endDay)
                ),
                TestContext.Current.CancellationToken
            );

        Assert.Equal(
            expectedPresent,
            preview.Warnings.IncompleteEmployees.Any(employee => employee.UserId == ResourceId)
        );
        Assert.Equal(expectedPresent ? 1 : 0, _databaseCommands.Count(command => command.Contains("\"FirstName\"")));
        Assert.DoesNotContain(_databaseCommands, command => command.Contains("\"Photo\""));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PublishAsync_WhenStandaloneShiftOverlapsCrossLocationDraftLeave_UsesCandidateDirection(
        bool reportOnShift
    )
    {
        _db.Users.Add(
            new User
            {
                Id = ResourceId,
                IdirName = "leave-user",
                IsEnabled = true,
                HomeLocationId = 5,
            }
        );
        _db.EventTypes.Add(new EventType { Code = "leave", Description = "Leave" });
        var leaveType = new LeaveType { Code = "Private", Description = "Private leave type" };
        _db.LeaveTypes.Add(leaveType);
        var leave = new LeaveEntry
        {
            LeaveType = leaveType,
            Event = new Event
            {
                Title = "Restricted details",
                SourceModule = "leave",
                EventTypeCode = "leave",
                StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
                StartAtUtc = ShiftStart.AddHours(-16),
                EndAtUtc = ShiftEnd.AddDays(1),
                AllDay = true,
                TimeZoneId = "America/Toronto",
                LocationId = 6,
            },
            Users = [new() { UserId = ResourceId }],
        };
        _db.LeaveEntries.Add(leave);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var shift = await SeedDraftShiftAsync();
        shift.Users.Add(new() { UserId = ResourceId });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var conflictService = new CalendarConflictService(
            [
                new SchedulingConflictParticipantProvider(
                    _db,
                    SchedulingLoanOutTestServices.CreateAvailability(_db, new TimeZoneService())
                ),
                new LeaveConflictParticipantProvider(_db),
            ],
            _db,
            CalendarConflictTestContext.Projection(ResourceId),
            CalendarConflictTestRules.Create(
                ("shift", reportOnShift ? ["leave"] : []),
                ("leave", reportOnShift ? [] : ["shift"])
            )
        );
        var service = CreateService([], conflictService: conflictService);
        var preview = await service.PreviewAsync(CreateRequest(), TestContext.Current.CancellationToken);
        if (!reportOnShift)
        {
            Assert.True(preview.CanPublish);
            Assert.Empty(preview.Blockers.Conflicts);
            await service.PublishAsync(CreateRequest(), null, TestContext.Current.CancellationToken);
            Assert.Equal(CalendarEventStatusTypeCodes.Active, shift.Event!.StatusTypeCode);
            return;
        }
        Assert.False(preview.CanPublish);
        var conflict = Assert.Single(preview.Blockers.Conflicts);
        Assert.Equal(ResourceId, conflict.ResourceId);
        Assert.Contains("Leave", Assert.IsType<string>(conflict.Title));
        var eventTitles = new[] { conflict.Entry!.Title, conflict.Overlaps!.Title };
        Assert.Contains("Leave", eventTitles);
        Assert.DoesNotContain("Restricted details", eventTitles);
        await Assert.ThrowsAsync<SchedulePublishBlockedException>(() =>
            service.PublishAsync(CreateRequest(), null, TestContext.Current.CancellationToken)
        );
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, shift.Event!.StatusTypeCode);
        await conflictService.CreateOverrideAsync(
            new CalendarConflictAcknowledgement
            {
                FirstSourceModule = "leave",
                FirstEventId = leave.EventId.ToString(),
                SecondSourceModule = "scheduling",
                SecondEventId = shift.EventId.ToString(),
                ResourceId = ResourceId,
                Note = "Approved",
            },
            ResourceId,
            TestContext.Current.CancellationToken
        );
        var result = await service.PublishAsync(CreateRequest(), null, TestContext.Current.CancellationToken);
        Assert.Equal(1, result.PublishedShiftEntryCount);
    }

    [Fact]
    public async Task PreviewAsync_WithUnassignedDraftCandidate_DoesNotReturnUnrelatedConflicts()
    {
        await SeedDraftShiftAsync();
        var conflict = new CalendarConflict(
            new CalendarConflictParticipant(
                "course-41",
                "training",
                ResourceId,
                ShiftStart,
                ShiftEnd,
                "Training",
                EventTypeCode: "training"
            ),
            new CalendarConflictParticipant(
                "course-42",
                "training",
                ResourceId,
                ShiftStart.AddHours(1),
                ShiftEnd,
                "Training",
                EventTypeCode: "training"
            ),
            ResourceId,
            ShiftStart.AddHours(1),
            ShiftEnd
        );
        var conflictService = new FakeCalendarConflictService([conflict]);
        var service = CreateService([], conflictService: conflictService);

        var preview = await service.PreviewAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.True(preview.CanPublish);
        Assert.Equal(1, preview.Candidates.ShiftEntryCount);
        Assert.Empty(preview.Blockers.Conflicts);
        Assert.Empty(
            Assert.IsAssignableFrom<IReadOnlyCollection<CalendarConflictParticipant>>(conflictService.LastCandidates)
        );
    }

    [Fact]
    public async Task PublishAsync_WithUnassignedDraftShift_WarnsAndPublishesShift()
    {
        var shiftEntry = await SeedDraftShiftAsync();
        var service = CreateService([]);
        var preview = await service.PreviewAsync(CreateRequest(), TestContext.Current.CancellationToken);
        Assert.Equal(shiftEntry.EventId, Assert.Single(preview.Warnings.UnassignedShifts).EventId);
        Assert.True(preview.CanPublish);

        var result = await service.PublishAsync(CreateRequest(), null, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.PublishedShiftEntryCount);
        Assert.Equal(0, result.PublishedAssignmentEntryCount);
        var eventEntity = await _db.Events.SingleAsync(
            candidate => candidate.Id == shiftEntry.EventId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CalendarEventStatusTypeCodes.Active, eventEntity.StatusTypeCode);
    }

    [Theory]
    [InlineData(nameof(Permissions.ShiftsView), true)]
    [InlineData(nameof(Permissions.AssignmentsView), false)]
    public async Task PreviewAsync_WithOneViewPermission_FiltersUnassignedWarnings(
        string permission,
        bool canViewShifts
    )
    {
        var assignment = await SeedLinkedDraftAssignmentAsync();
        var shift = await SeedDraftShiftAsync();
        var link = Assert.Single(assignment.ShiftAssignmentEntries);
        link.Users.Clear();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var accessor = CalendarConflictTestContext.Accessor(ResourceId);
        accessor.HttpContext!.User = CalendarConflictTestContext.Viewer(ResourceId, permission);
        var service = CreateService([], projection: CalendarConflictTestContext.Projection(accessor));

        var preview = await service.PreviewAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(
            canViewShifts ? [shift.EventId] : Array.Empty<int>(),
            preview.Warnings.UnassignedShifts.Select(item => item.EventId)
        );
        Assert.Equal(
            canViewShifts ? Array.Empty<int>() : [assignment.EventId],
            preview.Warnings.UnassignedAssignments.Select(item => item.EventId)
        );
        Assert.True(preview.CanPublish);
    }

    [Theory]
    [InlineData(1, 2, true)]
    [InlineData(-1, 1, true)]
    [InlineData(23, 25, true)]
    [InlineData(-1, 25, true)]
    [InlineData(-2, -1, false)]
    public async Task PreviewAsync_UsesCalendarOverlapSemantics(
        int startHourOffset,
        int endHourOffset,
        bool expectedInScope
    )
    {
        var rangeStart = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
        await SeedDraftShiftAsync(rangeStart.AddHours(startHourOffset), rangeStart.AddHours(endHourOffset));
        var service = CreateService([]);

        var preview = await service.PreviewAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(expectedInScope ? 1 : 0, preview.Candidates.ShiftEntryCount);
    }

    [Fact]
    public async Task PreviewAsync_OnlyIncludesTheRequestedLocation()
    {
        await SeedDraftShiftAsync();
        await SeedDraftShiftAsync(ShiftStart, ShiftEnd, 6);
        var service = CreateService([]);

        var preview = await service.PreviewAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(1, preview.Candidates.ShiftEntryCount);
    }

    [Fact]
    public async Task PreviewAsync_WithNoDraftCandidates_ExplainsStateThroughCountsAndDisablesPublication()
    {
        var preview = await CreateService([]).PreviewAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(0, preview.Candidates.TotalCount);
        Assert.False(preview.CanPublish);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PreviewAsync_WithMultipleShiftPublicationBlockers_OnlyReturnsAuthorizedDetails(bool canViewShifts)
    {
        await SeedLinkedDraftAssignmentAsync();
        var secondUserId = Guid.NewGuid();
        _db.Users.Add(
            new User
            {
                Id = secondUserId,
                IdirName = "second",
                FirstName = "Second",
                LastName = "Employee",
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var firstShift = new SchedulePublishShiftEventResponse(1, 10, "First shift", ShiftStart, ShiftEnd);
        var secondShift = new SchedulePublishShiftEventResponse(
            2,
            11,
            "Second shift",
            ShiftStart.AddHours(1),
            ShiftEnd
        );
        var blockers = new[]
        {
            new ShiftPublicationBlocker(
                ResourceId,
                ShiftConflictKind.StartDate,
                firstShift,
                secondShift,
                "First blocker"
            ),
            new ShiftPublicationBlocker(
                secondUserId,
                ShiftConflictKind.StartDate,
                firstShift,
                secondShift,
                "Second blocker"
            ),
        };
        var accessor = CalendarConflictTestContext.Accessor(ResourceId);
        if (!canViewShifts)
            accessor.HttpContext!.User = CalendarConflictTestContext.Viewer(
                ResourceId,
                nameof(Permissions.AssignmentsView)
            );
        var service = CreateService(
            [],
            shiftPublicationService: new FakeShiftPublicationService(_db, blockers),
            projection: CalendarConflictTestContext.Projection(accessor)
        );

        var preview = await service.PreviewAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.False(preview.CanPublish);
        Assert.Empty(preview.Warnings.IncompleteEmployees);
        if (canViewShifts)
        {
            Assert.Equal(["First blocker", "Second blocker"], preview.Blockers.ShiftConflicts.Select(x => x.Message));
            Assert.All(preview.Blockers.ShiftConflicts, blocker => Assert.Equal(firstShift, blocker.FirstShift));
            Assert.Contains(
                new SchedulePublishEmployeeResponse(secondUserId, "Second Employee"),
                preview.AffectedEmployees
            );
            Assert.Contains(preview.AffectedEmployees, employee => employee.UserId == ResourceId);
        }
        else
        {
            var restricted = Assert.Single(preview.Blockers.ShiftConflicts);
            Assert.Null(restricted.UserId);
            Assert.Null(restricted.Kind);
            Assert.Null(restricted.FirstShift);
            Assert.Null(restricted.SecondShift);
            Assert.Empty(preview.AffectedEmployees);
            Assert.DoesNotContain("Second blocker", restricted.Message);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task PreviewAsync_WithIncompleteSchedule_DistinguishesMissingItems(
        bool missingShift,
        bool missingAssignment
    )
    {
        var assignment = await SeedLinkedDraftAssignmentAsync();
        var shift = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .SingleAsync(TestContext.Current.CancellationToken);
        if (missingShift)
            shift.Event!.LocationId = 6;
        if (missingAssignment)
            assignment.Event!.LocationId = 6;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var preview = await CreateService([]).PreviewAsync(CreateRequest(), TestContext.Current.CancellationToken);

        var employee = Assert.Single(preview.Warnings.IncompleteEmployees);
        Assert.Equal(ResourceId, employee.UserId);
        Assert.Equal(missingShift, employee.MissingShift);
        Assert.Equal(missingAssignment, employee.MissingAssignment);
    }

    [Theory]
    [InlineData(1, 0, true, true, 5, 6, 10, 6, "UTC")]
    [InlineData(3, 0, true, true, 5, 6, 10, 6, "UTC")]
    [InlineData(5, 0, true, true, 5, 6, 10, 6, "UTC")]
    [InlineData(7, 0, false, true, 5, 6, 10, 6, "UTC")]
    [InlineData(1, 6, false, true, 5, 6, 10, 6, "UTC")]
    [InlineData(1, 6, true, false, 5, 6, 10, 6, "UTC")]
    [InlineData(1, 2, true, true, 5, 6, 10, 6, "UTC")]
    [InlineData(0, 7, false, true, 5, 6, 10, 6, "UTC")]
    [InlineData(7, 0, true, false, 5, 6, 10, 6, "UTC")]
    [InlineData(7, 0, true, true, 6, 5, 10, 6, "UTC")]
    [InlineData(7, 0, false, false, 6, 5, 10, 6, "UTC")]
    [InlineData(0, 7, false, false, 5, 6, 10, 6, "UTC")]
    [InlineData(5, 0, false, true, 5, 6, 11, 10, "UTC")]
    [InlineData(0, 5, false, true, 5, 6, 11, 10, "UTC")]
    [InlineData(1, 0, true, true, 5, 6, 11, 10, "UTC")]
    [InlineData(0, 1, true, true, 5, 6, 11, 10, "UTC")]
    [InlineData(2, 0, true, true, 5, 6, 11, 10, "UTC")]
    [InlineData(0, 2, true, true, 5, 6, 11, 10, "UTC")]
    [InlineData(5, 0, false, true, 5, 6, 11, 10, "America/Vancouver")]
    [InlineData(0, 5, false, true, 5, 6, 11, 10, "America/Vancouver")]
    public async Task PreviewAsync_ExcludesEmployeeOnlyWhenUnavailableEveryWeekday(
        int loanedDayMask,
        int leaveDayMask,
        bool expectedPresent,
        bool loanOutEnabled,
        int homeLocationId,
        int loanOutLocationId,
        int startMonth,
        int startDay,
        string timeZoneId
    )
    {
        var startDate = new DateOnly(2025, startMonth, startDay);
        var zones = new TimeZoneService();
        var eventStart = zones.ToUtcInstant(
            startDate.ToDateTime(new TimeOnly(9, 0)),
            zones.ResolveRequired(timeZoneId)
        );
        var location = await _db.Locations.SingleAsync(
            candidate => candidate.Id == 5,
            TestContext.Current.CancellationToken
        );
        location.Timezone = timeZoneId;
        _db.Users.Add(
            new User
            {
                Id = ResourceId,
                IdirName = "loaned-employee",
                FirstName = "Loaned",
                LastName = "Employee",
                IsEnabled = true,
                HomeLocationId = homeLocationId,
            }
        );
        _db.EventTypes.Add(
            new EventType
            {
                Id = 2,
                Code = LoanOutConstants.LoanOutEventTypeCode,
                Description = "Loan out",
                EffectiveDate = ShiftStart.AddYears(-1),
            }
        );
        _db.EventTypes.Add(
            new EventType
            {
                Id = 3,
                Code = LeaveConstants.LeaveEventTypeCode,
                Description = "Leave",
                EffectiveDate = ShiftStart.AddYears(-1),
            }
        );
        _db.LeaveTypes.Add(
            new LeaveType
            {
                Id = 1,
                Code = "test-leave",
                Description = "Test leave",
                EffectiveDate = ShiftStart.AddYears(-1),
            }
        );

        for (var dayOffset = 0; dayOffset < 3; dayOffset++)
        {
            if ((leaveDayMask & (1 << dayOffset)) != 0)
            {
                _db.LeaveEntries.Add(
                    new LeaveEntry
                    {
                        LeaveTypeId = 1,
                        Event = new Event
                        {
                            Title = "Leave",
                            SourceModule = LeaveConstants.SourceModule,
                            EventTypeCode = LeaveConstants.LeaveEventTypeCode,
                            StatusTypeCode = CalendarEventStatusTypeCodes.Active,
                            LocationId = 5,
                            StartAtUtc = eventStart.AddDays(dayOffset),
                            EndAtUtc = eventStart.AddDays(dayOffset).AddHours(4),
                        },
                        Users = [new LeaveEntryUser { UserId = ResourceId }],
                    }
                );
            }

            if ((loanedDayMask & (1 << dayOffset)) == 0)
                continue;

            _db.LoanOutEntries.Add(
                new LoanOutEntry
                {
                    LoanOutLocationId = loanOutLocationId,
                    Event = new Event
                    {
                        Title = "Loan out",
                        SourceModule = LoanOutConstants.SourceModule,
                        EventTypeCode = LoanOutConstants.LoanOutEventTypeCode,
                        StatusTypeCode = CalendarEventStatusTypeCodes.Active,
                        LocationId = homeLocationId,
                        StartAtUtc = eventStart.AddDays(dayOffset),
                        EndAtUtc = eventStart.AddDays(dayOffset).AddHours(4),
                    },
                    Users = [new LoanOutEntryUser { UserId = ResourceId }],
                }
            );
        }
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var preview = await CreateService([], loanOutEnabled: loanOutEnabled)
            .PreviewAsync(
                new SchedulePublishRequest(5, startDate, startDate.AddDays(2)),
                TestContext.Current.CancellationToken
            );

        Assert.Equal(
            expectedPresent ? [ResourceId] : [],
            preview.Warnings.IncompleteEmployees.Select(employee => employee.UserId)
        );
    }

    [Fact]
    public async Task PublishAsync_WithSeries_PublishesOnlyOverlappingOccurrence()
    {
        var eventSeries = new EventSeries
        {
            Title = "Draft series",
            StartAtUtc = ShiftStart,
            EndAtUtc = ShiftEnd,
            EventTypeCode = SchedulingConstants.ShiftEventTypeCode,
            StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
            LocationId = 5,
        };
        var shiftSeries = new ShiftSeries { EventSeries = eventSeries };
        var overlapping = await SeedDraftShiftAsync(ShiftStart, ShiftEnd, 5, shiftSeries, eventSeries);
        var outside = await SeedDraftShiftAsync(
            ShiftStart.AddDays(1),
            ShiftEnd.AddDays(1),
            5,
            shiftSeries,
            eventSeries
        );
        var service = CreateService([]);

        await service.PublishAsync(CreateRequest(), null, TestContext.Current.CancellationToken);

        _db.ChangeTracker.Clear();
        Assert.Equal(
            CalendarEventStatusTypeCodes.Active,
            (
                await _db.Events.FindAsync(new object?[] { overlapping.EventId }, TestContext.Current.CancellationToken)
            )!.StatusTypeCode
        );
        Assert.Equal(
            CalendarEventStatusTypeCodes.Draft,
            (
                await _db.Events.FindAsync(new object?[] { outside.EventId }, TestContext.Current.CancellationToken)
            )!.StatusTypeCode
        );
        Assert.Equal(
            CalendarEventStatusTypeCodes.Draft,
            (
                await _db.EventSeries.FindAsync(new object?[] { eventSeries.Id }, TestContext.Current.CancellationToken)
            )!.StatusTypeCode
        );
    }

    [Fact]
    public async Task PublishAsync_WhenDomainPublicationFails_RollsBackEarlierPublication()
    {
        var shiftEntry = await SeedDraftShiftAsync();
        var service = CreateService([], assignmentPublicationService: new ThrowingAssignmentPublicationService());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PublishAsync(CreateRequest(), null, TestContext.Current.CancellationToken)
        );

        _db.ChangeTracker.Clear();
        var eventEntity = await _db.Events.SingleAsync(
            candidate => candidate.Id == shiftEntry.EventId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, eventEntity.StatusTypeCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PublishAsync_RechecksTrainingConflictAndHonorsOverride(bool reportOnAssignment)
    {
        var assignment = await SeedLinkedDraftAssignmentAsync();
        var timeZoneService = new TimeZoneService();
        var timeZoneResolver = new CalendarTimeZoneResolver(
            Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "UTC" }),
            timeZoneService
        );
        var conflictService = new CalendarConflictService(
            [
                new SchedulingConflictParticipantProvider(
                    _db,
                    SchedulingLoanOutTestServices.CreateAvailability(_db, new TimeZoneService())
                ),
                new TrainingConflictParticipantProvider(_db, timeZoneResolver, timeZoneService),
            ],
            _db,
            CalendarConflictTestContext.Projection(ResourceId),
            CalendarConflictTestRules.Create(
                ("assignment", reportOnAssignment ? ["training"] : []),
                ("training", reportOnAssignment ? [] : ["assignment"])
            )
        );
        var service = CreateService([], conflictService: conflictService);
        Assert.True((await service.PreviewAsync(CreateRequest(), TestContext.Current.CancellationToken)).CanPublish);

        var training = new UserTraining
        {
            UserId = ResourceId,
            TrainingId = 1,
            AwardedOn = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero),
            EndingOn = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero),
            AllDay = true,
        };
        _db.UserTrainings.Add(training);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        if (!reportOnAssignment)
        {
            var preview = await service.PreviewAsync(CreateRequest(), TestContext.Current.CancellationToken);
            Assert.True(preview.CanPublish);
            Assert.Empty(preview.Blockers.Conflicts);
            await service.PublishAsync(CreateRequest(), null, TestContext.Current.CancellationToken);
            return;
        }

        var blocked = await Assert.ThrowsAsync<SchedulePublishBlockedException>(() =>
            service.PublishAsync(CreateRequest(), null, TestContext.Current.CancellationToken)
        );
        Assert.Contains(
            blocked.Preview.Blockers.Conflicts,
            conflict => conflict.FirstSourceModule == "training" || conflict.SecondSourceModule == "training"
        );
        Assert.Empty(blocked.Preview.Warnings.IncompleteEmployees);
        Assert.Equal(ResourceId, Assert.Single(blocked.Preview.AffectedEmployees).UserId);
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, assignment.Event!.StatusTypeCode);

        await conflictService.CreateOverrideAsync(
            new CalendarConflictAcknowledgement
            {
                FirstSourceModule = "training",
                FirstEventId = TrainingConflictParticipantProvider.IdentityFor(training.Id).EventId,
                SecondSourceModule = "scheduling",
                SecondEventId = assignment.EventId.ToString(),
                ResourceId = ResourceId,
                Note = "Approved",
            },
            ResourceId,
            TestContext.Current.CancellationToken
        );

        Assert.True((await service.PreviewAsync(CreateRequest(), TestContext.Current.CancellationToken)).CanPublish);
        await service.PublishAsync(CreateRequest(), null, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PreviewAsync_WithPublishedShiftAndAssignmentConflicts_ReportsWarningsWithoutBlocking(
        bool canViewAllEvents
    )
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var assignment = await SeedLinkedDraftAssignmentAsync();
        assignment.Event!.StatusTypeCode = CalendarEventStatusTypeCodes.Active;
        assignment.Event.Title = "Published assignment";
        var shift = await _db.ShiftEntries.Include(entry => entry.Event).SingleAsync(cancellationToken);
        shift.Event!.Title = "Private shift";
        _db.UserTrainings.Add(
            new UserTraining
            {
                UserId = ResourceId,
                TrainingId = 1,
                AwardedOn = ShiftStart.AddMinutes(17),
                EndingOn = ShiftStart.AddMinutes(42),
                AllDay = false,
            }
        );
        var draft = await SeedDraftShiftAsync();
        var timeZoneService = new TimeZoneService();
        var providers = new ICalendarConflictParticipantProvider[]
        {
            new SchedulingConflictParticipantProvider(
                _db,
                SchedulingLoanOutTestServices.CreateAvailability(_db, timeZoneService)
            ),
            new TrainingConflictParticipantProvider(
                _db,
                new CalendarTimeZoneResolver(
                    Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "UTC" }),
                    timeZoneService
                ),
                timeZoneService
            ),
        };
        var accessor = CalendarConflictTestContext.Accessor(ResourceId);
        if (!canViewAllEvents)
            accessor.HttpContext!.User = CalendarConflictTestContext.Viewer(
                ResourceId,
                nameof(Permissions.AssignmentsView)
            );
        var projection = new CalendarConflictProjection(providers, accessor);
        var conflictService = new CalendarConflictService(
            providers,
            _db,
            projection,
            CalendarConflictTestRules.Create(("assignment", ["training"]), ("shift", ["training"]))
        );
        var service = CreateService([], conflictService: conflictService, projection: projection);

        var preview = await service.PreviewAsync(CreateRequest(), cancellationToken);

        Assert.True(preview.CanPublish);
        Assert.Empty(preview.Blockers.Conflicts);
        Assert.False(preview.Blockers.HasRestrictedConflicts);
        Assert.Empty(preview.Warnings.IncompleteEmployees);
        Assert.Equal(ResourceId, Assert.Single(preview.AffectedEmployees).UserId);
        if (canViewAllEvents)
        {
            Assert.Equal(2, preview.Warnings.Conflicts.Count);
            Assert.Contains(preview.Warnings.Conflicts, conflict => conflict.Title!.Contains("Assignment"));
            Assert.Contains(preview.Warnings.Conflicts, conflict => conflict.Title!.Contains("Shift"));
            Assert.False(preview.Warnings.HasRestrictedConflicts);
        }
        else
        {
            Assert.Empty(preview.Warnings.Conflicts);
            Assert.True(preview.Warnings.HasRestrictedConflicts);
            Assert.True(preview.Warnings.HasUnattributedRestrictedConflicts);
            var warning = Assert.Single(preview.Warnings.RestrictedWarnings);
            var authorizedEvent = Assert.IsType<CalendarConflictEventResponse>(warning.AuthorizedEvent);
            Assert.Equal(assignment.EventId.ToString(), warning.EventId);
            Assert.Equal(warning.EventId, authorizedEvent.EventId);
            Assert.Equal("Published assignment", authorizedEvent.Title);
            Assert.Equal(ShiftStart, authorizedEvent.Start);
            Assert.Equal(ShiftEnd, authorizedEvent.End);
            var json = System.Text.Json.JsonSerializer.Serialize(preview);
            Assert.DoesNotContain("training.user-training.", json);
            Assert.DoesNotContain("FIRST-AID", json);
            Assert.DoesNotContain("Private shift", json);
            Assert.DoesNotContain("16:17:00", json);
        }

        var result = await service.PublishAsync(CreateRequest(), ResourceId, cancellationToken);
        Assert.Equal(1, result.PublishedShiftEntryCount);
        Assert.Equal(CalendarEventStatusTypeCodes.Active, draft.Event!.StatusTypeCode);
    }

    [Fact]
    public async Task Publication_WithMixedConflicts_FiltersWarningsAndPreservesAllCandidateBlockers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var assignment = await SeedLinkedDraftAssignmentAsync();
        var shift = await _db.ShiftEntries.Include(entry => entry.Event).SingleAsync(cancellationToken);
        var otherLocation = await SeedDraftShiftAsync(ShiftStart, ShiftEnd, 6);
        otherLocation.Event!.StatusTypeCode = CalendarEventStatusTypeCodes.Active;
        shift.Event!.EndAtUtc = assignment.Event!.EndAtUtc = ShiftEnd.AddHours(4);
        await _db.SaveChangesAsync(cancellationToken);

        var published = new CalendarConflictParticipant(
            shift.EventId.ToString(),
            "scheduling",
            ResourceId,
            ShiftStart,
            ShiftEnd.AddHours(4),
            "Published shift"
        );
        var draft = published with { EventId = assignment.EventId.ToString(), Title = "Draft assignment" };
        CalendarConflict ConflictWithTraining(
            CalendarConflictParticipant scheduleEvent,
            string trainingId,
            DateTimeOffset start
        ) =>
            new(
                scheduleEvent,
                new(trainingId, "training", ResourceId, start, start.AddHours(1), "Training"),
                ResourceId,
                start,
                start.AddHours(1),
                ReportOnEntry: true
            );

        var duplicate = new CalendarConflict(
            draft,
            published,
            ResourceId,
            ShiftStart,
            ShiftEnd.AddHours(4),
            ReportOnEntry: true,
            ReportOnOverlaps: true
        );
        var outsideRangeBlocker = ConflictWithTraining(draft, "candidate-next-day", ShiftEnd.AddHours(1));
        var warning = ConflictWithTraining(published, "in-scope", ShiftStart);
        var conflictService = new FakeCalendarConflictService(
            [duplicate, outsideRangeBlocker],
            [
                duplicate with
                {
                    Entry = published,
                    Overlaps = draft,
                },
                warning,
                warning,
                ConflictWithTraining(published, "outside", ShiftEnd),
                ConflictWithTraining(published, "overridden", ShiftStart) with
                {
                    IsOverridden = true,
                },
                ConflictWithTraining(published, "wrong-direction", ShiftStart) with
                {
                    ReportOnEntry = false,
                    ReportOnOverlaps = true,
                },
                ConflictWithTraining(draft, "draft-only", ShiftStart),
                ConflictWithTraining(
                    published with
                    {
                        EventId = otherLocation.EventId.ToString(),
                    },
                    "elsewhere",
                    ShiftStart
                ),
            ]
        );
        var service = CreateService([], conflictService: conflictService);

        var preview = await service.PreviewAsync(CreateRequest(), cancellationToken);
        var rejection = await Assert.ThrowsAsync<SchedulePublishBlockedException>(() =>
            service.PublishAsync(CreateRequest(), ResourceId, cancellationToken)
        );

        foreach (var response in new[] { preview, rejection.Preview })
        {
            Assert.False(response.CanPublish);
            Assert.Equal(
                new[] { duplicate.Id, outsideRangeBlocker.Id },
                response.Blockers.Conflicts.Select(conflict => conflict.Id)
            );
            Assert.Equal(warning.Id, Assert.Single(response.Warnings.Conflicts).Id);
        }
    }

    private async Task<AssignmentEntry> SeedLinkedDraftAssignmentAsync()
    {
        _db.EventTypes.Add(
            new EventType { Code = SchedulingConstants.AssignmentEventTypeCode, Description = "Assignment" }
        );
        _db.Users.Add(
            new User
            {
                Id = ResourceId,
                IdirName = "assigned-user",
                IsEnabled = true,
                HomeLocationId = 5,
            }
        );
        _db.Trainings.Add(
            new global::Unified.Db.Models.Training.Training
            {
                Id = 1,
                Code = "FIRST-AID",
                Description = "Course",
            }
        );
        _db.StatGroups.Add(new StatGroup { Id = 1, Name = "Group" });
        _db.StatCategories.Add(
            new StatCategory
            {
                Id = 1,
                GroupId = 1,
                Name = "Category",
            }
        );
        _db.SubCategories.Add(
            new SubCategory
            {
                Id = 1,
                CategoryId = 1,
                Name = "Subcategory",
            }
        );
        _db.AssignmentDefinitions.Add(
            new AssignmentDefinition
            {
                Id = 1,
                LocationId = 5,
                Name = "Assignment",
                NormalizedName = "assignment",
                DefaultCapacity = 1,
                CategoryId = 1,
                SubCategoryId = 1,
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var shift = await SeedDraftShiftAsync();
        shift.Event!.StatusTypeCode = CalendarEventStatusTypeCodes.Active;
        shift.Users.Add(new ShiftEntryUser { UserId = ResourceId });
        var assignment = new AssignmentEntry
        {
            AssignmentDefinitionId = 1,
            CategoryId = 1,
            SubCategoryId = 1,
            Capacity = 1,
            Event = new Event
            {
                Title = "Draft assignment",
                StartAtUtc = ShiftStart,
                EndAtUtc = ShiftEnd,
                EventTypeCode = SchedulingConstants.AssignmentEventTypeCode,
                StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
                SourceModule = SchedulingConstants.SourceModule,
                LocationId = 5,
            },
            ShiftAssignmentEntries =
            [
                new ShiftAssignmentEntry
                {
                    ShiftEntryId = shift.Id,
                    Users = [new ShiftAssignmentEntryUser { UserId = ResourceId }],
                },
            ],
        };
        _db.AssignmentEntries.Add(assignment);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return assignment;
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Publication_WhenConflictDetailsAreRestricted_OnlyReturnsAuthorizedAssociations(
        bool hasAuthorizedAnchor,
        bool hasUnattributedConflict
    )
    {
        var assignment = await SeedLinkedDraftAssignmentAsync();
        var shift = await _db
            .ShiftEntries.Include(entry => entry.Event)
            .SingleAsync(TestContext.Current.CancellationToken);
        var secondShift = await SeedDraftShiftAsync(ShiftStart, ShiftEnd.AddHours(1));
        secondShift.Event!.StatusTypeCode = CalendarEventStatusTypeCodes.Active;
        secondShift.Event.Title = "Authorized relief shift";
        secondShift.Users.Add(new ShiftEntryUser { UserId = ResourceId });
        shift.Event!.Title = "Authorized morning shift";
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var hiddenStart = ShiftStart.AddMinutes(17);
        var hidden = new CalendarConflict(
            new(
                "hidden-training-identity",
                "training",
                ResourceId,
                hiddenStart,
                hiddenStart.AddMinutes(13),
                "SECRET training"
            ),
            new(shift.EventId.ToString(), "scheduling", ResourceId, ShiftStart, ShiftEnd, "Visible shift"),
            ResourceId,
            hiddenStart,
            hiddenStart.AddMinutes(13)
        );
        var accessor = CalendarConflictTestContext.Accessor(ResourceId);
        accessor.HttpContext!.User = CalendarConflictTestContext.Viewer(ResourceId, nameof(Permissions.ShiftsView));
        var hiddenUserId = Guid.NewGuid();
        var conflicts = new List<CalendarConflict>();
        if (hasAuthorizedAnchor)
            conflicts.Add(
                hidden with
                {
                    Overlaps = hidden.Overlaps with
                    {
                        EventId = assignment.EventId.ToString(),
                        Title = "SECRET assignment",
                    },
                    ReportOnOverlaps = true,
                }
            );
        if (hasUnattributedConflict)
            conflicts.Add(
                hidden with
                {
                    Entry = hidden.Entry with { ResourceId = hiddenUserId },
                    Overlaps = hidden.Overlaps with { ResourceId = hiddenUserId },
                    ResourceId = hiddenUserId,
                    ReportOnEntry = true,
                }
            );
        var service = CreateService(
            [],
            conflictService: new FakeCalendarConflictService(
                conflicts,
                [
                    hidden with
                    {
                        ReportOnOverlaps = true,
                    },
                    hidden with
                    {
                        Overlaps = hidden.Overlaps with { EventId = secondShift.EventId.ToString() },
                        ReportOnOverlaps = true,
                    },
                ]
            ),
            projection: new CalendarConflictProjection(
                [
                    new SchedulingConflictParticipantProvider(
                        _db,
                        SchedulingLoanOutTestServices.CreateAvailability(_db, new TimeZoneService())
                    ),
                ],
                accessor
            )
        );

        var preview = await service.PreviewAsync(CreateRequest(), TestContext.Current.CancellationToken);
        var rejection = await Assert.ThrowsAsync<SchedulePublishBlockedException>(() =>
            service.PublishAsync(CreateRequest(), ResourceId, TestContext.Current.CancellationToken)
        );
        foreach (var response in new[] { preview, rejection.Preview })
        {
            Assert.False(response.CanPublish);
            Assert.Empty(response.Blockers.Conflicts);
            if (hasAuthorizedAnchor)
            {
                var warning = Assert.Single(response.Blockers.RestrictedWarnings);
                Assert.Equal(ResourceId, warning.ResourceId);
                Assert.Equal(shift.EventId.ToString(), warning.EventId);
            }
            else
            {
                Assert.Empty(response.Blockers.RestrictedWarnings);
            }
            var attributedFinding = Assert.Single(
                response.Blockers.RestrictedWarnings.Concat(response.Warnings.RestrictedWarnings),
                warning => warning.EventId == shift.EventId.ToString()
            );
            var distinctWarning = Assert.Single(
                response.Warnings.RestrictedWarnings,
                warning => warning.EventId == secondShift.EventId.ToString()
            );
            Assert.Equal("Authorized relief shift", distinctWarning.AuthorizedEvent?.Title);
            Assert.True(response.Warnings.HasRestrictedConflicts);
            var authorizedEvent = Assert.IsType<CalendarConflictEventResponse>(attributedFinding.AuthorizedEvent);
            Assert.Equal(shift.EventId.ToString(), authorizedEvent.EventId);
            Assert.Equal("Authorized morning shift", authorizedEvent.Title);
            Assert.Equal(ShiftStart, authorizedEvent.Start);
            Assert.Equal(ShiftEnd, authorizedEvent.End);
            Assert.Equal(ResourceId, Assert.Single(response.AffectedEmployees).UserId);
            Assert.True(response.Blockers.HasRestrictedConflicts);
            Assert.Equal(hasUnattributedConflict, response.Blockers.HasUnattributedRestrictedConflicts);
            var json = System.Text.Json.JsonSerializer.Serialize(response);
            Assert.DoesNotContain(hiddenUserId.ToString(), json);
            Assert.DoesNotContain("hidden-training-identity", json);
            Assert.DoesNotContain("SECRET", json);
            Assert.DoesNotContain(hiddenStart.ToString("yyyy-MM-ddTHH:mm:ss"), json);
        }
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, assignment.Event!.StatusTypeCode);
        Assert.Equal(CalendarEventStatusTypeCodes.Active, shift.Event!.StatusTypeCode);
    }

    private SchedulePublishService CreateService(
        IReadOnlyCollection<CalendarConflict> conflicts,
        IAssignmentPublicationService? assignmentPublicationService = null,
        IShiftPublicationService? shiftPublicationService = null,
        ICalendarConflictService? conflictService = null,
        bool loanOutEnabled = true,
        CalendarConflictProjection? projection = null
    ) =>
        new(
            _db,
            SchedulingLoanOutTestServices.CreateAvailability(_db, new TimeZoneService()),
            new CalendarTimeZoneResolver(
                Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "America/Vancouver" }),
                new TimeZoneService()
            ),
            new TimeZoneService(),
            shiftPublicationService ?? new FakeShiftPublicationService(_db),
            assignmentPublicationService ?? new FakeAssignmentPublicationService(),
            conflictService ?? new FakeCalendarConflictService(conflicts),
            NullLogger<SchedulePublishService>.Instance,
            SchedulingLoanOutTestServices.CreateAvailability(
                _db,
                new TimeZoneService(),
                loanOutEnabled: loanOutEnabled
            ),
            new StatutoryHolidayCalendarDataProvider(new BcStatutoryHolidayCalculator(), new TimeZoneService()),
            projection ?? CalendarConflictTestContext.Projection(ResourceId),
            leaveDataProvider: new LeaveCalendarDataProvider(
                _db,
                new SchedulingLeaveEventMapper(new CalendarEventMapper(), new TimeZoneService())
            ),
            schedulingCalendarDataProvider: new LoanOutCalendarDataProvider(
                _db,
                new LoanOutCalendarMapper(),
                new TimeZoneService()
            )
        );

    private static SchedulePublishRequest CreateRequest() =>
        new(5, new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 6));

    private Task<ShiftEntry> SeedDraftShiftAsync() => SeedDraftShiftAsync(ShiftStart, ShiftEnd);

    private Task<ShiftEntry> SeedDraftShiftAsync(DateTimeOffset startAtUtc, DateTimeOffset endAtUtc) =>
        SeedDraftShiftAsync(startAtUtc, endAtUtc, 5);

    private async Task<ShiftEntry> SeedDraftShiftAsync(
        DateTimeOffset startAtUtc,
        DateTimeOffset endAtUtc,
        int locationId,
        ShiftSeries? shiftSeries = null,
        EventSeries? eventSeries = null
    )
    {
        var shiftEntry = new ShiftEntry
        {
            ShiftSeries = shiftSeries,
            Event = new Event
            {
                EventSeries = eventSeries,
                Title = "Draft shift",
                StartAtUtc = startAtUtc,
                EndAtUtc = endAtUtc,
                EventTypeCode = SchedulingConstants.ShiftEventTypeCode,
                StatusTypeCode = CalendarEventStatusTypeCodes.Draft,
                SourceModule = SchedulingConstants.SourceModule,
                LocationId = locationId,
            },
        };
        _db.ShiftEntries.Add(shiftEntry);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return shiftEntry;
    }

    private async Task SeedBaseDataAsync()
    {
        _db.EventTypes.Add(
            new EventType
            {
                Id = 1,
                Code = SchedulingConstants.ShiftEventTypeCode,
                Description = "Shift",
                EffectiveDate = ShiftStart.AddYears(-1),
            }
        );
        _db.EventStatusTypes.AddRange(
            new EventStatusType
            {
                Id = 1,
                Code = CalendarEventStatusTypeCodes.Draft,
                Description = "Draft",
                EffectiveDate = ShiftStart.AddYears(-1),
            },
            new EventStatusType
            {
                Id = 2,
                Code = CalendarEventStatusTypeCodes.Active,
                Description = "Active",
                EffectiveDate = ShiftStart.AddYears(-1),
            }
        );
        _db.Locations.AddRange(
            new Location
            {
                Id = 5,
                AgencyId = "A5",
                Name = "Five",
                Timezone = "UTC",
            },
            new Location
            {
                Id = 6,
                AgencyId = "A6",
                Name = "Six",
                Timezone = "UTC",
            }
        );
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}

internal sealed class FakeCalendarConflictService(
    IReadOnlyCollection<CalendarConflict> conflicts,
    IReadOnlyCollection<CalendarConflict>? scopedConflicts = null
) : ICalendarConflictService
{
    public IReadOnlyCollection<CalendarConflictParticipant>? LastCandidates { get; private set; }

    public Task<IReadOnlyCollection<CalendarConflict>> GetConflictsAsync(
        CalendarConflictQuery query,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(scopedConflicts ?? conflicts);

    public Task<IReadOnlyCollection<CalendarConflict>> GetConflictsForCandidatesAsync(
        IReadOnlyCollection<CalendarConflictParticipant> candidates,
        CancellationToken cancellationToken = default
    )
    {
        LastCandidates = candidates;
        return Task.FromResult(candidates.Count == 0 ? [] : conflicts);
    }

    public Task EnsureNoUnresolvedConflictsAsync(
        IReadOnlyCollection<CalendarConflictParticipant> candidates,
        CancellationToken cancellationToken = default
    )
    {
        _ = conflicts.Count;
        return Task.CompletedTask;
    }

    public Task ValidateAndApplyConflictAcknowledgementsAsync(
        IReadOnlyCollection<CalendarConflictParticipant> candidates,
        IReadOnlyCollection<CalendarConflictAcknowledgement>? acknowledgements,
        Guid? actorId,
        CancellationToken cancellationToken = default
    )
    {
        _ = conflicts.Count;
        return Task.CompletedTask;
    }

    public Task CreateOverrideAsync(
        CalendarConflictAcknowledgement acknowledgement,
        Guid createdById,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();

    public Task InvalidateResolvedOverridesAsync(
        IReadOnlyCollection<CalendarConflictEventIdentity> eventIdentities,
        Guid? updatedById = null,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();
}

internal sealed class FakeShiftPublicationService(
    UnifiedDbContext db,
    IReadOnlyCollection<ShiftPublicationBlocker>? blockers = null
) : IShiftPublicationService
{
    public Task<IReadOnlyCollection<ShiftPublicationBlocker>> GetEntryPublicationBlockersAsync(
        IReadOnlyCollection<int> shiftEntryIds,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(blockers ?? []);

    public async Task<ShiftPublicationResult> PublishEntriesAsync(
        IReadOnlyCollection<int> shiftEntryIds,
        CancellationToken cancellationToken = default
    )
    {
        var entries = await db
            .ShiftEntries.Include(entry => entry.Event)
            .Where(entry => shiftEntryIds.Contains(entry.Id))
            .ToListAsync(cancellationToken);
        foreach (var entry in entries)
            entry.Event!.StatusTypeCode = CalendarEventStatusTypeCodes.Active;
        await db.SaveChangesAsync(cancellationToken);
        return new ShiftPublicationResult(entries, []);
    }
}

internal sealed class FakeAssignmentPublicationService : IAssignmentPublicationService
{
    public Task<IReadOnlyCollection<AssignmentEntry>> PublishEntriesAsync(
        IReadOnlyCollection<int> assignmentEntryIds,
        CancellationToken cancellationToken = default
    ) => Task.FromResult<IReadOnlyCollection<AssignmentEntry>>([]);
}

internal sealed class ThrowingAssignmentPublicationService : IAssignmentPublicationService
{
    public Task<IReadOnlyCollection<AssignmentEntry>> PublishEntriesAsync(
        IReadOnlyCollection<int> assignmentEntryIds,
        CancellationToken cancellationToken = default
    ) => throw new InvalidOperationException("Assignment publication failed.");
}
