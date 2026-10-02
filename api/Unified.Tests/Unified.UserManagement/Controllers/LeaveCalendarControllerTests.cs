using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Unified.UserManagement.Controllers;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;
using Unified.UserManagement.Validators;

namespace Unified.Tests.UserManagement.Controllers;

public class LeaveCalendarControllerTests
{
    [Fact]
    public async Task GetData_Should_Return_Ok_With_CalendarData()
    {
        // Arrange
        var expected = new LeaveCalendarDataResponse
        {
            Events =
            [
                new()
                {
                    Id = "user-management.leave.1",
                    LeaveId = 1,
                    EventId = 10,
                    UserId = Guid.NewGuid(),
                    Type = "user-management.leave",
                    SourceModule = "user-management",
                    Title = "Leave - Vacation",
                    Start = DateTimeOffset.UtcNow,
                    EventTypeCode = "leave",
                    StatusTypeCode = "active",
                    LeaveTypeCode = "VAC",
                },
            ],
        };
        var fakeService = new FakeLeaveCalendarService { Result = expected };
        var controller = new LeaveCalendarController(fakeService, new LeaveCalendarRequestValidator());
        var request = new LeaveCalendarRequest
        {
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 2),
        };

        // Act
        var result = await controller.GetData(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, okResult.Value);
        Assert.Same(request, fakeService.LastRequest);
    }

    [Fact]
    public async Task GetData_Should_Throw_Validation_Error_When_EndDate_Before_StartDate()
    {
        // Arrange
        var controller = new LeaveCalendarController(
            new FakeLeaveCalendarService(),
            new LeaveCalendarRequestValidator()
        );
        var request = new LeaveCalendarRequest
        {
            StartDate = new DateOnly(2026, 6, 2),
            EndDate = new DateOnly(2026, 6, 1),
        };

        // Act + Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.GetData(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task GetData_Should_Throw_Validation_Error_When_TimeZoneId_Invalid()
    {
        // Arrange
        var controller = new LeaveCalendarController(
            new FakeLeaveCalendarService(),
            new LeaveCalendarRequestValidator()
        );
        var request = new LeaveCalendarRequest
        {
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 2),
            TimeZoneId = "Not/A_Real_Zone",
        };

        // Act + Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.GetData(request, TestContext.Current.CancellationToken)
        );
    }

    private sealed class FakeLeaveCalendarService : ILeaveCalendarService
    {
        public LeaveCalendarDataResponse Result { get; init; } = new() { Events = [] };
        public LeaveCalendarRequest? LastRequest { get; private set; }

        public Task<LeaveCalendarDataResponse> GetLeaveCalendarDataAsync(
            LeaveCalendarRequest request,
            CancellationToken cancellationToken = default
        )
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }
}
