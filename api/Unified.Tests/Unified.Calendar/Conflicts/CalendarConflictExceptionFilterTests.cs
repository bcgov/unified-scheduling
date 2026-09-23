using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Models;

namespace Unified.Tests.Calendar.Conflicts;

public sealed class CalendarConflictExceptionFilterTests
{
    [Fact]
    public void OnException_WithCalendarConflictException_ReturnsConflictResponse()
    {
        var resourceId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 9, 21, 16, 0, 0, TimeSpan.Zero);
        var conflict = new CalendarConflict(
            new CalendarConflictParticipant(1, "scheduling", resourceId, start, start.AddHours(1), "First"),
            new CalendarConflictParticipant(
                2,
                "scheduling",
                resourceId,
                start.AddMinutes(30),
                start.AddHours(2),
                "Second"
            ),
            resourceId,
            start.AddMinutes(30),
            start.AddHours(1)
        );
        var exception = new CalendarConflictException([conflict]);
        var context = new ExceptionContext(
            new ActionContext(
                new DefaultHttpContext(),
                new RouteData(),
                new ActionDescriptor(),
                new ModelStateDictionary()
            ),
            []
        )
        {
            Exception = exception,
        };

        new CalendarConflictExceptionFilter().OnException(context);

        var result = Assert.IsType<ConflictObjectResult>(context.Result);
        var response = Assert.IsType<CalendarConflictRejectionResponse>(result.Value);
        Assert.True(context.ExceptionHandled);
        Assert.Equal(exception.Message, response.Message);
        Assert.Equal(conflict.Id, Assert.Single(response.Conflicts).Id);
    }
}
