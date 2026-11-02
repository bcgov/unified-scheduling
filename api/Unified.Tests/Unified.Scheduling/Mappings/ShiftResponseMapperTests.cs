using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Mappings;

namespace Unified.Tests.Scheduling.Mappings;

public sealed class ShiftResponseMapperTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CalendarResponseHidesOnlyAwayUsersWithoutChangingSourceExceptions(bool allAway)
    {
        var awayUser = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var shift = new ShiftEntry
        {
            Event = new Event
            {
                Title = "Shift",
                StartAtUtc = DateTimeOffset.UtcNow,
                IsException = true,
                ExceptionReason = "Other",
            },
            Users = [new ShiftEntryUser { UserId = awayUser }, new ShiftEntryUser { UserId = otherUser }],
        };

        var response = ShiftResponseMapper.ToCalendarEventResponse(shift, allAway ? [awayUser, otherUser] : [awayUser]);

        Assert.Equal(2, response.UserIds.Count);
        Assert.Equal(allAway ? [] : new[] { otherUser.ToString() }, response.ResourceIds);
        Assert.True(response.IsException);
        Assert.Equal("Other", response.ExceptionReason);
        Assert.Equal("Other", shift.Event.ExceptionReason);
        Assert.Equal(2, shift.Users.Count);
    }

    [Theory]
    [InlineData(true, "IsAway")]
    [InlineData(true, "Other")]
    [InlineData(false, null)]
    public void EntryResponsePreservesExceptionFlags(bool isException, string? reason)
    {
        var shift = new ShiftEntry
        {
            Id = 42,
            Event = new Event
            {
                Title = "Shift",
                StartAtUtc = DateTimeOffset.UtcNow,
                SourceModule = "scheduling",
                EventTypeCode = "shift",
                StatusTypeCode = "Draft",
                IsException = isException,
                ExceptionReason = reason,
            },
        };

        var response = ShiftResponseMapper.ToShiftEntryResponse(shift);

        Assert.Equal(42, response.Id);
        Assert.Equal(isException, response.IsException);
        Assert.Equal(reason, response.ExceptionReason);

        var calendarResponse = ShiftResponseMapper.ToCalendarEventResponse(shift);
        Assert.Equal(isException, calendarResponse.IsException);
        Assert.Equal(reason, calendarResponse.ExceptionReason);
    }
}
