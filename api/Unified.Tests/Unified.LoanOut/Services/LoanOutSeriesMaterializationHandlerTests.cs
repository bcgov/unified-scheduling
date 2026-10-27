using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Unified.Calendar.Services;
using Unified.Db;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
using Unified.LoanOut.Models;
using Unified.LoanOut.Services;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.LoanOut.Services;

public sealed class LoanOutSeriesMaterializationHandlerTests : IAsyncLifetime
{
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");
    private SqliteConnection _connection = null!;
    private UnifiedDbContext _db = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.CreateFunction("now", () => DateTimeOffset.UtcNow.ToString("O"));
        await _connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(_connection).Options;
        _db = new SqliteTestUnifiedDbContext(options);
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task OnMaterializedEventCreatedAsync_CreatesEntryWithSeriesLocationAndUsers()
    {
        var series = new LoanOutSeries { LoanOutLocationId = 10 };
        var handler = new LoanOutSeriesMaterializationHandler(_db);
        var eventEntity = new Event { Title = "Occurrence" };
        var context = new LoanOutSeriesMaterializationContext { LoanOutSeries = series, UserIds = [UserA] };

        await handler.OnMaterializedEventCreatedAsync(
            new EventSeries(),
            eventEntity,
            new SeriesEntry
            {
                StartAtUtc = DateTimeOffset.Parse("2026-06-01T16:00:00Z"),
                EndAtUtc = DateTimeOffset.Parse("2026-06-01T17:00:00Z"),
            },
            context,
            TestContext.Current.CancellationToken
        );

        var entry = Assert.Single(_db.LoanOutEntries.Local);
        Assert.Same(series, entry.LoanOutSeries);
        Assert.Same(eventEntity, entry.Event);
        Assert.Equal(10, entry.LoanOutLocationId);
        Assert.Equal(UserA, Assert.Single(entry.Users).UserId);
    }

    [Fact]
    public async Task OnMaterializedEventsDeletingAsync_RemovesOnlyEntriesForDeletedEvents()
    {
        var removedEvent = new Event { Id = 1, Title = "Removed" };
        var retainedEvent = new Event { Id = 2, Title = "Retained" };
        var removedEntry = new LoanOutEntry
        {
            Id = 1,
            EventId = removedEvent.Id,
            Event = removedEvent,
            LoanOutLocationId = 10,
            Users = [new LoanOutEntryUser { Id = 1, UserId = UserA }],
        };
        var retainedEntry = new LoanOutEntry
        {
            Id = 2,
            EventId = retainedEvent.Id,
            Event = retainedEvent,
            LoanOutLocationId = 10,
            Users = [new LoanOutEntryUser { Id = 2, UserId = UserA }],
        };
        _db.LoanOutEntries.AttachRange(removedEntry, retainedEntry);
        var handler = new LoanOutSeriesMaterializationHandler(_db);
        var context = new LoanOutSeriesMaterializationContext
        {
            LoanOutSeries = new LoanOutSeries(),
            UserIds = [UserA],
            ExistingEntries = [removedEntry, retainedEntry],
        };

        await handler.OnMaterializedEventsDeletingAsync(
            new EventSeries(),
            [removedEvent],
            context,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(EntityState.Deleted, _db.Entry(removedEntry).State);
        Assert.Equal(EntityState.Deleted, _db.Entry(removedEntry.Users.Single()).State);
        Assert.Equal(EntityState.Unchanged, _db.Entry(retainedEntry).State);
        Assert.Equal(EntityState.Unchanged, _db.Entry(retainedEntry.Users.Single()).State);
    }
}
