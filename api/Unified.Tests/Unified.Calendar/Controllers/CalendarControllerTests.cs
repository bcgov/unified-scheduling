using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Calendar;
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
                    Id = 10,
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
            new CalendarDataRequestValidator()
        );

        // Act
        var result = await controller.GetData(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CalendarDataResponse>(okResult.Value);
        var item = Assert.Single(payload.Events);

        Assert.Equal("calendar", payload.ModuleId);
        Assert.Equal("calendar.events", payload.ContributionId);
        Assert.Equal(10, item.Id);
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
            new CalendarDataRequestValidator()
        );

        // Act / Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.GetData(request, TestContext.Current.CancellationToken)
        );
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
}
