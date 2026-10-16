using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Common.Validation;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Leave;
using Unified.Db.Models.UserManagement;
using Unified.Leave.Constants;
using Unified.Leave.Models;
using Unified.Leave.Services;

namespace Unified.Tests.Leave.Services;

public sealed class LeaveTypeServiceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2025, 3, 3, 9, 0, 0, TimeSpan.Zero);

    private LeaveTestDatabase _database = null!;
    private LeaveTypeService _service = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await LeaveTestDatabase.CreateAsync();
        _service = new LeaveTypeService(NullLogger<LeaveTypeService>.Instance, _database.Db);
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task CreateLeaveTypeAsync_WhenNameHasWhitespace_TrimsCodeAndDefaultsEffectiveDate()
    {
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        var result = await _service.CreateLeaveTypeAsync(
            CreateRequest("  Vacation  "),
            TestContext.Current.CancellationToken
        );

        Assert.Equal("Vacation", result.Name);
        var entity = await _database.Db.LeaveTypes.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Vacation", entity.Code);
        Assert.True(entity.IsPaid);
        Assert.Equal(DateTimeOffset.UtcNow.UtcDateTime.Date, entity.EffectiveDate.UtcDateTime);
    }

    [Fact]
    public async Task CreateLeaveTypeAsync_WhenDatesHaveTimeAndOffset_NormalizesToUtcMidnight()
    {
        // 2026-10-21 17:00 -07:00 is 2026-10-22 00:00 UTC.
        var effective = new DateTimeOffset(2020, 6, 9, 17, 0, 0, TimeSpan.FromHours(-7));
        var expiry = new DateTimeOffset(2026, 10, 21, 17, 30, 0, TimeSpan.FromHours(-7));

        await _service.CreateLeaveTypeAsync(
            CreateRequest("LWOP") with
            {
                EffectiveDateUtc = effective,
                ExpiryDateUtc = expiry,
            },
            TestContext.Current.CancellationToken
        );

        var entity = await _database.Db.LeaveTypes.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new DateTimeOffset(2020, 6, 10, 0, 0, 0, TimeSpan.Zero), entity.EffectiveDate);
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero), entity.ExpiryDate);
    }

    [Fact]
    public async Task UpdateLeaveTypeAsync_WhenDatesHaveTime_NormalizesToUtcMidnightAndClearsExpiry()
    {
        var created = await _service.CreateLeaveTypeAsync(
            CreateRequest("Vacation") with
            {
                ExpiryDateUtc = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
            TestContext.Current.CancellationToken
        );

        await _service.UpdateLeaveTypeAsync(
            created.Id,
            CreateRequest("Vacation") with
            {
                EffectiveDateUtc = new DateTimeOffset(2026, 3, 5, 14, 15, 0, TimeSpan.Zero),
                ExpiryDateUtc = new DateTimeOffset(2027, 3, 5, 23, 59, 0, TimeSpan.Zero),
            },
            TestContext.Current.CancellationToken
        );

        var entity = await _database.Db.LeaveTypes.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new DateTimeOffset(2026, 3, 5, 0, 0, 0, TimeSpan.Zero), entity.EffectiveDate);
        Assert.Equal(new DateTimeOffset(2027, 3, 5, 0, 0, 0, TimeSpan.Zero), entity.ExpiryDate);

        await _service.UpdateLeaveTypeAsync(
            created.Id,
            CreateRequest("Vacation"),
            TestContext.Current.CancellationToken
        );

        entity = await _database.Db.LeaveTypes.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Null(entity.ExpiryDate);
    }

    [Fact]
    public async Task CreateLeaveTypeAsync_WhenEffectiveDateProvided_UsesIt()
    {
        var effective = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        await _service.CreateLeaveTypeAsync(
            CreateRequest("Sick") with
            {
                EffectiveDateUtc = effective,
            },
            TestContext.Current.CancellationToken
        );

        var entity = await _database.Db.LeaveTypes.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(effective, entity.EffectiveDate);
    }

    [Fact]
    public async Task CreateLeaveTypeAsync_WhenCodeAlreadyExists_ThrowsConflict()
    {
        await _service.CreateLeaveTypeAsync(CreateRequest("Vacation"), TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<ConflictValidationException>(() =>
            _service.CreateLeaveTypeAsync(CreateRequest(" Vacation "), TestContext.Current.CancellationToken)
        );

        Assert.Contains(nameof(LeaveTypeRequest.Name), ex.Errors.Keys);
        Assert.Equal(1, await _database.Db.LeaveTypes.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateLeaveTypeAsync_WhenNotFound_ReturnsNull()
    {
        var result = await _service.UpdateLeaveTypeAsync(
            999,
            CreateRequest("Vacation"),
            TestContext.Current.CancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateLeaveTypeAsync_WhenCodeUsedByAnotherLeaveType_ThrowsConflict()
    {
        await _service.CreateLeaveTypeAsync(CreateRequest("Vacation"), TestContext.Current.CancellationToken);
        var other = await _service.CreateLeaveTypeAsync(CreateRequest("Sick"), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ConflictValidationException>(() =>
            _service.UpdateLeaveTypeAsync(other.Id, CreateRequest("Vacation"), TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task UpdateLeaveTypeAsync_WhenValid_UpdatesFieldsAndAllowsKeepingOwnCode()
    {
        var created = await _service.CreateLeaveTypeAsync(
            CreateRequest("Vacation"),
            TestContext.Current.CancellationToken
        );

        var result = await _service.UpdateLeaveTypeAsync(
            created.Id,
            CreateRequest("Vacation") with
            {
                Description = "Updated",
                IsPaid = false,
            },
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        var entity = await _database.Db.LeaveTypes.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Vacation", entity.Code);
        Assert.Equal("Updated", entity.Description);
        Assert.False(entity.IsPaid);
    }

    [Fact]
    public async Task DeleteLeaveTypeAsync_WhenNotFound_ReturnsFalse()
    {
        var result = await _service.DeleteLeaveTypeAsync(999, TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteLeaveTypeAsync_WhenUnused_DeletesAndReturnsTrue()
    {
        var created = await _service.CreateLeaveTypeAsync(
            CreateRequest("Vacation"),
            TestContext.Current.CancellationToken
        );

        var result = await _service.DeleteLeaveTypeAsync(created.Id, TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Empty(_database.Db.LeaveTypes);
    }

    [Fact]
    public async Task DeleteLeaveTypeAsync_WhenUsedByLeaveEntry_ThrowsConflict()
    {
        var leaveTypeId = await CreateLeaveTypeAsync();
        var eventEntity = AddEvent();
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _database.Db.LeaveEntries.Add(new LeaveEntry { LeaveTypeId = leaveTypeId, EventId = eventEntity.Id });
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await AssertDeleteConflictAsync(leaveTypeId);
    }

    [Fact]
    public async Task DeleteLeaveTypeAsync_WhenUsedByLeaveSeries_ThrowsConflict()
    {
        var leaveTypeId = await CreateLeaveTypeAsync();
        var eventSeries = new EventSeries
        {
            Title = "Series",
            EventTypeCode = LeaveConstants.LeaveEventTypeCode,
            StartAtUtc = Start,
        };
        _database.Db.EventSeries.Add(eventSeries);
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _database.Db.LeaveSeries.Add(new LeaveSeries { LeaveTypeId = leaveTypeId, EventSeriesId = eventSeries.Id });
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await AssertDeleteConflictAsync(leaveTypeId);
    }

    private async Task AssertDeleteConflictAsync(int leaveTypeId)
    {
        await Assert.ThrowsAsync<ConflictValidationException>(() =>
            _service.DeleteLeaveTypeAsync(leaveTypeId, TestContext.Current.CancellationToken)
        );
        Assert.True(
            await _database.Db.LeaveTypes.AnyAsync(x => x.Id == leaveTypeId, TestContext.Current.CancellationToken)
        );
    }

    private async Task<int> CreateLeaveTypeAsync()
    {
        var created = await _service.CreateLeaveTypeAsync(
            CreateRequest("Vacation"),
            TestContext.Current.CancellationToken
        );
        return created.Id;
    }

    private Event AddEvent()
    {
        var eventEntity = new Event
        {
            Title = "Leave",
            EventTypeCode = LeaveConstants.LeaveEventTypeCode,
            SourceModule = LeaveConstants.SourceModule,
            StartAtUtc = Start,
            EndAtUtc = Start.AddHours(8),
        };
        _database.Db.Events.Add(eventEntity);
        return eventEntity;
    }

    private static LeaveTypeRequest CreateRequest(string name) =>
        new()
        {
            Name = name,
            Description = "Description",
            IsPaid = true,
        };
}
