using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
using Unified.LoanOut.Factories;
using Unified.LoanOut.Models;

namespace Unified.Tests.LoanOut.Factories;

public sealed class LoanOutEventFactoryTests
{
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserB = new("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void CreateEntry_CopiesLoanOutFieldsAndRemovesDuplicateUsers()
    {
        var eventEntity = new Event();
        var request = new LoanOutEntryRequest
        {
            Title = "Loan out",
            StartAtUtc = DateTimeOffset.Parse("2026-06-01T16:00:00Z"),
            LoanOutLocationId = 10,
            LoanOutSeriesId = 15,
            UserIds = [UserA, UserB, UserA],
        };

        var result = new LoanOutEventFactory().CreateEntry(request, eventEntity);

        Assert.Same(eventEntity, result.Event);
        Assert.Equal(10, result.LoanOutLocationId);
        Assert.Equal(15, result.LoanOutSeriesId);
        Assert.Equal([UserA, UserB], result.Users.Select(user => user.UserId));
    }

    [Fact]
    public void UpdateEntry_SynchronizesUsersAndLoanOutFields()
    {
        var existing = new LoanOutEntry
        {
            LoanOutLocationId = 5,
            LoanOutSeriesId = 6,
            Users = [new LoanOutEntryUser { UserId = UserA }, new LoanOutEntryUser { UserId = UserB }],
        };
        var request = new LoanOutEntryRequest
        {
            Title = "Updated",
            StartAtUtc = DateTimeOffset.Parse("2026-06-01T16:00:00Z"),
            LoanOutLocationId = 10,
            UserIds = [UserB, UserB],
        };

        var result = new LoanOutEventFactory().UpdateEntry(request, existing);

        Assert.Same(existing, result);
        Assert.Equal(10, result.LoanOutLocationId);
        Assert.Null(result.LoanOutSeriesId);
        Assert.Equal([UserB], result.Users.Select(user => user.UserId));
    }

    [Fact]
    public void CreateSeries_CopiesLoanOutFieldsAndRemovesDuplicateUsers()
    {
        var eventSeries = new EventSeries();
        var request = new LoanOutSeriesRequest
        {
            Title = "Series",
            RecurrenceRule = "FREQ=DAILY;COUNT=2",
            StartAtUtc = DateTimeOffset.Parse("2026-06-01T16:00:00Z"),
            EndAtUtc = DateTimeOffset.Parse("2026-06-01T17:00:00Z"),
            LoanOutLocationId = 10,
            UserIds = [UserA, UserB, UserA],
        };

        var result = new LoanOutEventFactory().CreateSeries(request, eventSeries);

        Assert.Same(eventSeries, result.EventSeries);
        Assert.Equal(10, result.LoanOutLocationId);
        Assert.Equal([UserA, UserB], result.Users.Select(user => user.UserId));
    }
}
