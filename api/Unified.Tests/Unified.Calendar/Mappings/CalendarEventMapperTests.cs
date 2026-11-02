using Unified.Calendar.Mappings;
using Unified.Db.Models.Calendar;
using Unified.Scheduling.Models;

namespace Unified.Tests.Calendar.Mappings;

public sealed class CalendarEventMapperTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApplyToEvent_WhenSchedulingRequestHasVersion_PreservesCalendarVersion(bool assignment)
    {
        var mapper = new CalendarEventMapper();
        var entity = new Event { ConcurrencyToken = 91, Title = "Original" };

        if (assignment)
            mapper.ApplyToEvent(entity, new AssignmentEntryUpdateRequest { ConcurrencyToken = 7, Title = " Updated " });
        else
            mapper.ApplyToEvent(entity, new ShiftEntryUpdateRequest { ConcurrencyToken = 7, Title = " Updated " });

        Assert.Equal(91u, entity.ConcurrencyToken);
        Assert.Equal("Updated", entity.Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApplyToEventSeries_WhenSchedulingRequestHasVersion_PreservesCalendarVersion(bool assignment)
    {
        var mapper = new CalendarEventMapper();
        var entity = new EventSeries { ConcurrencyToken = 92, Title = "Original" };

        if (assignment)
            mapper.ApplyToEventSeries(entity, new AssignmentSeriesUpdateRequest { ConcurrencyToken = 7, Title = " Updated " });
        else
            mapper.ApplyToEventSeries(entity, new ShiftSeriesUpdateRequest { ConcurrencyToken = 7, Title = " Updated " });

        Assert.Equal(92u, entity.ConcurrencyToken);
        Assert.Equal("Updated", entity.Title);
    }
}
