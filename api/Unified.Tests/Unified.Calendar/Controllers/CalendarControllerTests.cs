using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Authorization.Claims;
using Unified.Calendar;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Controllers;
using Unified.Calendar.Models;
using Unified.Calendar.Services;
using Unified.Calendar.Validators;

namespace Unified.Tests.Calendar.Controllers;

public class CalendarControllerTests
{
    [Fact]
    public async Task GetEvents_WhenRequestIsValid_ReturnsOkResult()
    {
        // Arrange
        var request = new CalendarDataRequest
        {
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 2),
        };

        var expected = new CalendarDataResponse
        {
            Events =
            [
                new()
                {
                    Id = "10",
                    Title = "Holiday",
                    StartAtUtc = new DateTimeOffset(2026, 6, 1, 7, 0, 0, TimeSpan.Zero),
                    SourceModule = "calendar",
                    EventTypeCode = CalendarEventTypeCode.Holiday,
                    StatusTypeCode = CalendarEventStatusTypeCode.Active,
                },
            ],
        };

        var service = new FakeCalendarEventService { Result = expected };
        var controller = new CalendarController(
            NullLogger<CalendarController>.Instance,
            service,
            new FakeCalendarConflictService(),
            new CalendarDataRequestValidator(),
            new CalendarConflictAcknowledgementValidator()
        );

        // Act
        var result = await controller.GetData(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CalendarDataResponse>(okResult.Value);
        var item = Assert.Single(payload.Events);

        Assert.Equal("calendar", payload.ModuleId);
        Assert.Equal("calendar.events", payload.ContributionId);
        Assert.Equal("10", item.Id);
        Assert.Equal(request, service.LastRequest);
    }

    [Fact]
    public async Task GetEvents_WhenRequestIsInvalid_ThrowsValidationException()
    {
        // Arrange
        var request = new CalendarDataRequest
        {
            StartDate = new DateOnly(2026, 6, 2),
            EndDate = new DateOnly(2026, 6, 1),
        };

        var controller = new CalendarController(
            NullLogger<CalendarController>.Instance,
            new FakeCalendarEventService(),
            new FakeCalendarConflictService(),
            new CalendarDataRequestValidator(),
            new CalendarConflictAcknowledgementValidator()
        );

        // Act / Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.GetData(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task CreateConflictOverride_WhenRequestIsValid_ReturnsNoContent()
    {
        // Arrange
        var request = new CalendarConflictAcknowledgement
        {
            FirstEventId = 10,
            SecondEventId = 20,
            ResourceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Note = "Approved overlap",
        };
        var conflictService = new FakeCalendarConflictService();
        var controller = new CalendarController(
            NullLogger<CalendarController>.Instance,
            new FakeCalendarEventService(),
            conflictService,
            new CalendarDataRequestValidator(),
            new CalendarConflictAcknowledgementValidator()
        );
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity([new Claim(UnifiedClaimTypes.UserId, "22222222-2222-2222-2222-222222222222")])
                ),
            },
        };

        // Act
        var result = await controller.CreateConflictOverride(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.Equal(request, conflictService.LastOverrideRequest);
    }

    private sealed class FakeCalendarEventService : ICalendarEventService
    {
        public CalendarDataResponse Result { get; init; } = new();

        public CalendarDataRequest? LastRequest { get; private set; }

        public Task<CalendarDataResponse> GetCalendarDataAsync(
            CalendarDataRequest request,
            CancellationToken cancellationToken = default
        )
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeCalendarConflictService : ICalendarConflictService
    {
        public CalendarConflictAcknowledgement? LastOverrideRequest { get; private set; }

        public Task<IReadOnlyCollection<CalendarConflict>> GetConflictsAsync(
            CalendarConflictQuery query,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyCollection<CalendarConflict>>([]);

        public Task EnsureNoUnresolvedConflictsAsync(
            IReadOnlyCollection<CalendarConflictParticipant> candidates,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task ValidateAndApplyConflictAcknowledgementsAsync(
            IReadOnlyCollection<CalendarConflictParticipant> candidates,
            IReadOnlyCollection<CalendarConflictAcknowledgement>? acknowledgements,
            Guid? actorId,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task CreateOverrideAsync(
            CalendarConflictAcknowledgement acknowledgement,
            Guid? createdById,
            CancellationToken cancellationToken = default
        )
        {
            LastOverrideRequest = acknowledgement;
            return Task.CompletedTask;
        }

        public Task InvalidateResolvedOverridesAsync(
            IReadOnlyCollection<int> eventIds,
            Guid? updatedById = null,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }
}
