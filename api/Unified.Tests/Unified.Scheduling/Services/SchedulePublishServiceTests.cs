using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Models;
using Unified.Calendar.Options;
using Unified.Calendar.Services;
using Unified.Common.Calendar.Conflicts;
using Unified.Common.Time;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.Scheduling.Services;

public sealed class SchedulePublishServiceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset ShiftStart = new(2026, 10, 6, 16, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ShiftEnd = ShiftStart.AddHours(8);
    private static readonly Guid ResourceId = new("11111111-1111-1111-1111-111111111111");

    private SqliteConnection _connection = null!;
    private UnifiedDbContext _db = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(_connection).Options;
        _db = new SqliteTestUnifiedDbContext(options);
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await SeedBaseDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task PreviewAsync_WithUnassignedDraftCandidate_DoesNotReturnUnrelatedConflicts()
    {
        await SeedDraftShiftAsync();
        var conflict = new CalendarConflict(
            new CalendarConflictParticipant("course-41", "training", ResourceId, ShiftStart, ShiftEnd, "Training"),
            new CalendarConflictParticipant(
                "course-42",
                "training",
                ResourceId,
                ShiftStart.AddHours(1),
                ShiftEnd,
                "Training"
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
    public async Task PublishAsync_WithEligibleDraftShift_PublishesShift()
    {
        var shiftEntry = await SeedDraftShiftAsync();
        var service = CreateService([]);

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

    [Fact]
    public async Task PreviewAsync_WithMultipleShiftPublicationBlockers_ReturnsEveryBlocker()
    {
        await SeedDraftShiftAsync();
        var blockers = new[]
        {
            new ShiftPublicationBlocker("First blocker"),
            new ShiftPublicationBlocker("Second blocker"),
        };
        var service = CreateService([], shiftPublicationService: new FakeShiftPublicationService(_db, blockers));

        var preview = await service.PreviewAsync(CreateRequest(), TestContext.Current.CancellationToken);

        Assert.False(preview.CanPublish);
        Assert.Equal(["First blocker", "Second blocker"], preview.Blockers.ShiftConflicts.Select(x => x.Message));
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
            (await _db.Events.FindAsync(overlapping.EventId))!.StatusTypeCode
        );
        Assert.Equal(CalendarEventStatusTypeCodes.Draft, (await _db.Events.FindAsync(outside.EventId))!.StatusTypeCode);
        Assert.Equal(
            CalendarEventStatusTypeCodes.Draft,
            (await _db.EventSeries.FindAsync(eventSeries.Id))!.StatusTypeCode
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

    private SchedulePublishService CreateService(
        IReadOnlyCollection<CalendarConflict> conflicts,
        IAssignmentPublicationService? assignmentPublicationService = null,
        IShiftPublicationService? shiftPublicationService = null,
        ICalendarConflictService? conflictService = null
    ) =>
        new(
            _db,
            new CalendarTimeZoneResolver(
                Options.Create(new CalendarDateTimeOptions { DefaultTimeZoneId = "America/Vancouver" }),
                new TimeZoneService()
            ),
            new TimeZoneService(),
            shiftPublicationService ?? new FakeShiftPublicationService(_db),
            assignmentPublicationService ?? new FakeAssignmentPublicationService(),
            conflictService ?? new FakeCalendarConflictService(conflicts),
            NullLogger<SchedulePublishService>.Instance
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

internal sealed class FakeCalendarConflictService(IReadOnlyCollection<CalendarConflict> conflicts)
    : ICalendarConflictService
{
    public IReadOnlyCollection<CalendarConflictParticipant>? LastCandidates { get; private set; }

    public Task<IReadOnlyCollection<CalendarConflict>> GetConflictsAsync(
        CalendarConflictQuery query,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(conflicts);

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
