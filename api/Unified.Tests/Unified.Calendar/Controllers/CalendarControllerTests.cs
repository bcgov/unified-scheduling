using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
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
        var request = new CalendarEventsRequest
        {
            StartDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero),
        };

        var expected = new List<CalendarEventResponse>
        {
            new()
            {
                Id = 10,
                Title = "Holiday",
                StartAtUtc = request.StartDate,
                SourceModule = "calendar",
                EventTypeCode = "holiday",
                StatusTypeCode = "active",
            },
        };

        var service = new FakeCalendarEventService { Result = expected };
        var controller = new CalendarController(
            NullLogger<CalendarController>.Instance,
            service,
            new CalendarEventsRequestValidator()
        );

        // Act
        var result = await controller.GetEvents(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyCollection<CalendarEventResponse>>(okResult.Value);
        var item = Assert.Single(payload);

        Assert.Equal(10, item.Id);
        Assert.Equal(request, service.LastRequest);
    }

    [Fact]
    public async Task GetEvents_WhenRequestIsInvalid_ThrowsValidationException()
    {
        // Arrange
        var request = new CalendarEventsRequest
        {
            StartDate = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
        };

        var controller = new CalendarController(
            NullLogger<CalendarController>.Instance,
            new FakeCalendarEventService(),
            new CalendarEventsRequestValidator()
        );

        // Act / Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => controller.GetEvents(request, TestContext.Current.CancellationToken)
        );
    }

    private sealed class FakeCalendarEventService : ICalendarEventService
    {
        public IReadOnlyCollection<CalendarEventResponse> Result { get; init; } = [];

        public CalendarEventsRequest? LastRequest { get; private set; }

        public Task<IReadOnlyCollection<CalendarEventResponse>> GetEventsAsync(
            CalendarEventsRequest request,
            CancellationToken cancellationToken = default
        )
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }
}
