using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Models;
using Unified.Common.Calendar.Conflicts;

namespace Unified.Tests.Calendar.Conflicts;

public sealed class CalendarConflictExceptionFilterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OnException_TrainingConflict_FiltersUnauthorizedDetails(bool canViewTraining)
    {
        var resourceId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 9, 21, 16, 0, 0, TimeSpan.Zero);
        var conflict = new CalendarConflict(
            new CalendarConflictParticipant(
                "training.user-training.7",
                "training",
                resourceId,
                start,
                start.AddDays(1),
                "Training: SECRET"
            ),
            new CalendarConflictParticipant("42", "scheduling", resourceId, start, start.AddHours(1), "Assignment"),
            resourceId,
            start,
            start.AddHours(1)
        );
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    canViewTraining
                        ? [new Claim(UnifiedClaimTypes.Permission, nameof(Permissions.UserTrainingsView))]
                        : []
                )
            ),
        };
        var context = new ExceptionContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), new ModelStateDictionary()),
            []
        )
        {
            Exception = new CalendarConflictException([conflict]),
        };

        new CalendarConflictExceptionFilter().OnException(context);

        var response = Assert.IsType<CalendarConflictRejectionResponse>(
            Assert.IsType<ConflictObjectResult>(context.Result).Value
        );
        Assert.Equal(canViewTraining, response.Conflicts.Any());
        if (!canViewTraining)
            Assert.DoesNotContain("SECRET", System.Text.Json.JsonSerializer.Serialize(response));
    }

    [Fact]
    public void OnException_WithCalendarConflictException_ReturnsConflictResponse()
    {
        var resourceId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 9, 21, 16, 0, 0, TimeSpan.Zero);
        var conflict = new CalendarConflict(
            new CalendarConflictParticipant("1", "scheduling", resourceId, start, start.AddHours(1), "First"),
            new CalendarConflictParticipant(
                "2",
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
