using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Common.Validation;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.TimeOff;
using Unified.Db.Models.UserManagement;
using Unified.TimeOff.Constants;
using Unified.TimeOff.Models;
using Unified.TimeOff.Services;

namespace Unified.Tests.TimeOff.Services;

public sealed class LeaveTypeServiceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2025, 3, 3, 9, 0, 0, TimeSpan.Zero);

    private TimeOffTestDatabase _database = null!;
    private LeaveTypeService _service = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await TimeOffTestDatabase.CreateAsync();
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
        Assert.True(entity.EffectiveDate >= before);
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
    public async Task DeleteLeaveTypeAsync_WhenUsedByTimeOffEntry_ThrowsConflict()
    {
        var leaveTypeId = await CreateLeaveTypeAsync();
        var eventEntity = AddEvent();
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _database.Db.TimeOffEntries.Add(new TimeOffEntry { LeaveTypeId = leaveTypeId, EventId = eventEntity.Id });
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await AssertDeleteConflictAsync(leaveTypeId);
    }

    [Fact]
    public async Task DeleteLeaveTypeAsync_WhenUsedByTimeOffSeries_ThrowsConflict()
    {
        var leaveTypeId = await CreateLeaveTypeAsync();
        var eventSeries = new EventSeries
        {
            Title = "Series",
            EventTypeCode = TimeOffConstants.TimeOffEventTypeCode,
            StartAtUtc = Start,
        };
        _database.Db.EventSeries.Add(eventSeries);
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _database.Db.TimeOffSeries.Add(new TimeOffSeries { LeaveTypeId = leaveTypeId, EventSeriesId = eventSeries.Id });
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
            EventTypeCode = TimeOffConstants.TimeOffEventTypeCode,
            SourceModule = TimeOffConstants.SourceModule,
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
