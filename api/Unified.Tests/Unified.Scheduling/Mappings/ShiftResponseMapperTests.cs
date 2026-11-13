using Unified.Db.Models.Calendar;
using Unified.Db.Models.Scheduling;
using Unified.Scheduling.Mappings;

namespace Unified.Tests.Scheduling.Mappings;

public sealed class ShiftResponseMapperTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Responses_WhenLoanOutDisabled_ExcludeOnlyAwayWithoutChangingPersistedReasons(bool timeChanged)
    {
        var shift = new ShiftEntry
        {
            Event = new Event { ExceptionReasonCodes = timeChanged ? ["DateTimeChanged", "IsAway"] : ["IsAway"] },
        };
        var entry = ShiftResponseMapper.ToShiftEntryResponse(shift, loanOutEnabled: false);
        var calendar = ShiftResponseMapper.ToCalendarEventResponse(shift, loanOutEnabled: false);
        Assert.Equal(timeChanged, entry.IsException);
        Assert.Equal(timeChanged, calendar.IsException);
        Assert.Equal(
            timeChanged ? new[] { "DateTimeChanged" } : [],
            entry.ExceptionReasonCodes.Select(code => code.ToString())
        );
        Assert.Equal(entry.ExceptionReasonCodes, calendar.ExceptionReasonCodes);
        Assert.Contains("IsAway", shift.Event.ExceptionReasonCodes);
        Assert.True(shift.Event.IsException);
    }

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
                ExceptionReasonCodes = ["DateTimeChanged"],
            },
            Users = [new ShiftEntryUser { UserId = awayUser }, new ShiftEntryUser { UserId = otherUser }],
        };

        var response = ShiftResponseMapper.ToCalendarEventResponse(shift, allAway ? [awayUser, otherUser] : [awayUser]);

        Assert.Equal(2, response.UserIds.Count);
        Assert.Equal(allAway ? [] : new[] { otherUser.ToString() }, response.ResourceIds);
        Assert.True(response.IsException);
        Assert.Equal(new[] { "DateTimeChanged" }, response.ExceptionReasonCodes.Select(code => code.ToString()));
        Assert.Equal(new[] { "DateTimeChanged" }, shift.Event.ExceptionReasonCodes.Select(code => code.ToString()));
        Assert.Equal(2, shift.Users.Count);
    }

    [Theory]
    [InlineData(true, "IsAway")]
    [InlineData(true, "DateTimeChanged")]
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
                ExceptionReasonCodes = reason is null ? [] : [reason],
            },
        };

        var response = ShiftResponseMapper.ToShiftEntryResponse(shift);

        Assert.Equal(42, response.Id);
        Assert.Equal(isException, response.IsException);
        Assert.Equal(
            (reason is null ? Array.Empty<string>() : new[] { reason }),
            response.ExceptionReasonCodes.Select(code => code.ToString())
        );

        var calendarResponse = ShiftResponseMapper.ToCalendarEventResponse(shift);
        Assert.Equal(isException, calendarResponse.IsException);
        Assert.Equal(
            (reason is null ? Array.Empty<string>() : new[] { reason }),
            calendarResponse.ExceptionReasonCodes.Select(code => code.ToString())
        );
    }
}
