using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Db.Models.Lookup;
using Unified.TimeOff.Constants;
using Unified.TimeOff.Seeders;

namespace Unified.Tests.TimeOff.Seeders;

public sealed class TimeOffSeedersTests : IAsyncLifetime
{
    private TimeOffTestDatabase _database = null!;

    public async ValueTask InitializeAsync() => _database = await TimeOffTestDatabase.CreateAsync();

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task LeaveTypeSeeder_SeedAsync_InsertsAllConfiguredLeaveTypes()
    {
        await new LeaveTypeSeeder(NullLogger<LeaveTypeSeeder>.Instance).SeedAsync(
            _database.Db,
            TestContext.Current.CancellationToken
        );

        var rows = await _database.Db.LeaveTypes.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(LeaveTypeConstants.LeaveTypes.Keys.Order(), rows.Select(x => x.Code).Order());
        foreach (var (code, details) in LeaveTypeConstants.LeaveTypes)
        {
            var row = rows.Single(x => x.Code == code);
            Assert.Equal(details.Description, row.Description);
            Assert.Equal(details.IsPaid, row.IsPaid);
            Assert.Null(row.ExpiryDate);
        }
    }

    [Fact]
    public async Task LeaveTypeSeeder_SeedAsync_WhenRunTwice_DoesNotDuplicate()
    {
        var seeder = new LeaveTypeSeeder(NullLogger<LeaveTypeSeeder>.Instance);

        await seeder.SeedAsync(_database.Db, TestContext.Current.CancellationToken);
        await seeder.SeedAsync(_database.Db, TestContext.Current.CancellationToken);

        Assert.Equal(
            LeaveTypeConstants.LeaveTypes.Count,
            await _database.Db.LeaveTypes.CountAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task LeaveTypeSeeder_SeedAsync_ResetsExistingRow()
    {
        _database.Db.LeaveTypes.Add(
            new LeaveType
            {
                Code = "LWOP",
                Description = "Old",
                IsPaid = true,
                EffectiveDate = new DateTimeOffset(2019, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ExpiryDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await new LeaveTypeSeeder(NullLogger<LeaveTypeSeeder>.Instance).SeedAsync(
            _database.Db,
            TestContext.Current.CancellationToken
        );

        var row = await _database.Db.LeaveTypes.SingleAsync(
            x => x.Code == "LWOP",
            TestContext.Current.CancellationToken
        );
        Assert.Equal("Leave Without Pay", row.Description);
        Assert.False(row.IsPaid);
        Assert.Equal(new DateTimeOffset(2020, 6, 10, 0, 0, 0, TimeSpan.Zero), row.EffectiveDate);
        Assert.Null(row.ExpiryDate);
    }

    [Fact]
    public async Task EventTypeSeeder_SeedAsync_WhenMissing_AddsTimeOffEventType()
    {
        _database.Db.EventTypes.RemoveRange(
            _database.Db.EventTypes.Where(x => x.Code == TimeOffConstants.TimeOffEventTypeCode)
        );
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await new SchedulingTimeOffEventTypeSeeder(NullLogger<SchedulingTimeOffEventTypeSeeder>.Instance).SeedAsync(
            _database.Db,
            TestContext.Current.CancellationToken
        );

        var row = await _database.Db.EventTypes.SingleAsync(
            x => x.Code == TimeOffConstants.TimeOffEventTypeCode,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(TimeOffConstants.TimeOffEventTypeDescription, row.Description);
    }

    [Fact]
    public async Task EventTypeSeeder_SeedAsync_WhenExisting_UpdatesAndClearsExpiry()
    {
        var existing = await _database.Db.EventTypes.SingleAsync(
            x => x.Code == TimeOffConstants.TimeOffEventTypeCode,
            TestContext.Current.CancellationToken
        );
        existing.Description = "Old";
        existing.ExpiryDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await _database.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var seeder = new SchedulingTimeOffEventTypeSeeder(NullLogger<SchedulingTimeOffEventTypeSeeder>.Instance);
        await seeder.SeedAsync(_database.Db, TestContext.Current.CancellationToken);
        await seeder.SeedAsync(_database.Db, TestContext.Current.CancellationToken);

        var rows = await _database
            .Db.EventTypes.Where(x => x.Code == TimeOffConstants.TimeOffEventTypeCode)
            .ToListAsync(TestContext.Current.CancellationToken);
        var row = Assert.Single(rows);
        Assert.Equal(TimeOffConstants.TimeOffEventTypeDescription, row.Description);
        Assert.Null(row.ExpiryDate);
    }
}
