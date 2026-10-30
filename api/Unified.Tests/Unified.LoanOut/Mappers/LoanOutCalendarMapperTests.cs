using Unified.Db.Models.Calendar;
using Unified.Db.Models.LoanOut;
using Unified.Db.Models.UserManagement;
using Unified.LoanOut.Mappers;
using Unified.LoanOut.Models.Calendar;
using Unified.Scheduling.Models;

namespace Unified.Tests.LoanOut.Mappers;

public sealed class LoanOutCalendarMapperTests
{
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void ToLoanOutCalendarEvent_MapsEventAndLoanOutFields()
    {
        var start = DateTimeOffset.Parse("2026-06-01T16:00:00Z");
        var entry = new LoanOutEntry
        {
            Id = 12,
            LoanOutSeriesId = 7,
            LoanOutLocationId = 10,
            EventId = 30,
            Users = [new LoanOutEntryUser { UserId = UserA }, new LoanOutEntryUser { UserId = UserA }],
            Event = new Event
            {
                Title = "Loan out",
                StartAtUtc = start,
                EndAtUtc = start.AddHours(2),
                SourceModule = "scheduling",
                EventTypeCode = "LOAN_OUT",
                StatusTypeCode = "ACTIVE",
            },
        };

        var result = new LoanOutCalendarMapper().ToLoanOutCalendarEvent(entry);

        Assert.Equal("scheduling.LoanOut-entry.12", result.Id);
        Assert.Equal(12, result.LoanOutEntryId);
        Assert.Equal(7, result.LoanOutSeriesId);
        Assert.Equal(10, result.LoanOutLocationId);
        Assert.Equal(30, result.EventId);
        Assert.Equal("scheduling.LoanOut", result.Type);
        Assert.Equal(start, result.Start);
        Assert.Equal(start.AddHours(2), result.End);
        Assert.Equal([UserA], result.UserIds);
    }

    [Fact]
    public void ToLoanOutCalendarEvent_WhenEventIsMissing_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new LoanOutCalendarMapper().ToLoanOutCalendarEvent(new LoanOutEntry { Id = 12 })
        );

        Assert.Contains("has no associated event", exception.Message);
    }

    [Fact]
    public void ToLoanOutCalendarUser_MapsAndOrdersEntryMetadata()
    {
        var earlier = DateTimeOffset.Parse("2026-06-01T16:00:00Z");
        var user = new User
        {
            Id = UserA,
            IdirName = "test.user",
            IsEnabled = true,
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            Gender = Gender.Other,
        };
        var entries = new[]
        {
            CreateEntry(2, earlier.AddDays(1), 20, "FREQ=WEEKLY"),
            CreateEntry(1, earlier, 10, null),
        };

        var result = new LoanOutCalendarMapper().ToLoanOutCalendarUser(user, entries);

        Assert.Equal(UserA, result.Id);
        Assert.Equal(string.Empty, result.EmployeeNumber);
        Assert.Collection(
            result.LoanOutMetaData,
            first =>
            {
                Assert.Equal(10, first.LoanOutLocationId);
                Assert.Equal(earlier, first.StartAtUtc);
                Assert.Null(first.RecurrenceRule);
            },
            second =>
            {
                Assert.Equal(20, second.LoanOutLocationId);
                Assert.Equal(earlier.AddDays(1), second.StartAtUtc);
                Assert.Equal("FREQ=WEEKLY", second.RecurrenceRule);
            }
        );
    }

    [Fact]
    public void ToResponse_MapsCalendarEventFields()
    {
        var start = DateTimeOffset.Parse("2026-06-01T16:00:00Z");
        var eventData = new LoanOutCalendarEvent
        {
            Id = "scheduling.LoanOut-entry.12",
            EventId = 30,
            UserIds = [UserA],
            Type = "scheduling.LoanOut",
            SourceModule = "scheduling",
            Title = "Loan out",
            Start = start,
            End = start.AddHours(2),
            EventTypeCode = "LOAN_OUT",
            StatusTypeCode = "ACTIVE",
        };

        var result = LoanOutCalendarMapper.ToResponse(eventData);

        Assert.Equal(eventData.Id, result.Id);
        Assert.Equal(eventData.EventId, result.EventId);
        Assert.Equal(eventData.UserIds, result.UserIds);
        Assert.Equal(eventData.Type, result.Type);
        Assert.Equal(eventData.SourceModule, result.SourceModule);
        Assert.Equal(eventData.Title, result.Title);
        Assert.Equal(eventData.Start, result.Start);
        Assert.Equal(eventData.End, result.End);
        Assert.Equal(eventData.EventTypeCode, result.EventTypeCode);
        Assert.Equal(eventData.StatusTypeCode, result.StatusTypeCode);
    }

    private static LoanOutEntry CreateEntry(int id, DateTimeOffset start, int locationId, string? recurrenceRule)
    {
        var eventSeries = recurrenceRule is null ? null : new EventSeries { RecurrenceRule = recurrenceRule };
        return new LoanOutEntry
        {
            Id = id,
            LoanOutLocationId = locationId,
            Event = new Event
            {
                StartAtUtc = start,
                EndAtUtc = start.AddHours(2),
                EventSeries = eventSeries,
            },
        };
    }
}
