using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Core.Pdf;
using Unified.Scheduling.Controllers;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;
using Unified.Scheduling.Validators;

namespace Unified.Tests.Scheduling.Controllers;

public sealed class ScheduleDistributionControllerTests
{
    [Fact]
    public async Task Distribute_WhenRenderingConfigurationIsUnavailable_ReturnsServiceUnavailable()
    {
        var controller = CreateController(
            new FailingDistributionService(
                new PdfRenderingException(
                    "Invalid rendering configuration.",
                    new InvalidOperationException(),
                    isServiceUnavailable: true
                )
            ),
            Permissions.ShiftsView
        );

        var result = await controller.Distribute(CreateRequest(), TestContext.Current.CancellationToken);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
        Assert.Equal("Schedule distribution is unavailable.", Assert.IsType<ProblemDetails>(problem.Value).Title);
    }

    [Fact]
    public async Task Distribute_WhenRenderingFails_ReturnsBadGateway()
    {
        var controller = CreateController(
            new FailingDistributionService(new PdfRenderingException("Rendering failed.")),
            Permissions.ShiftsView
        );

        var result = await controller.Distribute(CreateRequest(), TestContext.Current.CancellationToken);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, problem.StatusCode);
    }

    [Theory]
    [InlineData("schedule", Permissions.AssignmentsView)]
    [InlineData("assignments", Permissions.ShiftsView)]
    public async Task Distribute_WithoutPermissionForRequestedView_ReturnsForbidden(string view, Permissions permission)
    {
        var service = new RecordingDistributionService();
        var controller = CreateController(service, permission);

        var result = await controller.Distribute(CreateRequest(view), TestContext.Current.CancellationToken);

        Assert.IsType<ForbidResult>(result.Result);
        Assert.Equal(0, service.CallCount);
    }

    [Theory]
    [InlineData("schedule", Permissions.ShiftsView)]
    [InlineData("assignments", Permissions.AssignmentsView)]
    public async Task Distribute_WithPermissionForRequestedView_InvokesService(string view, Permissions permission)
    {
        var service = new RecordingDistributionService();
        var controller = CreateController(service, permission);

        var result = await controller.Distribute(CreateRequest(view), TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(1, service.CallCount);
    }

    [Fact]
    public async Task Distribute_WhenPeriodRangeIsMisaligned_RejectsBeforeInvokingService()
    {
        var service = new RecordingDistributionService();
        var controller = CreateController(service, Permissions.ShiftsView);
        var request = CreateRequest() with
        {
            Context = CreateRequest().Context! with
            {
                Period = "week",
                StartDate = new DateOnly(2026, 8, 25),
                EndDate = new DateOnly(2026, 9, 1),
            },
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.Distribute(request, TestContext.Current.CancellationToken)
        );
        Assert.Equal(0, service.CallCount);
    }

    private static ScheduleDistributionController CreateController(
        IScheduleDistributionService service,
        params Permissions[] permissions
    )
    {
        var claims = permissions.Select(permission => new Claim(UnifiedClaimTypes.Permission, permission.ToString()));
        return new ScheduleDistributionController(
            service,
            new ScheduleDistributionRequestValidator(),
            NullLogger<ScheduleDistributionController>.Instance
        )
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) },
            },
        };
    }

    private static ScheduleDistributionRequest CreateRequest(string view = "schedule") =>
        new()
        {
            Context = new ScheduleDistributionContext
            {
                View = view,
                LocationId = 7,
                StartDate = new DateOnly(2026, 8, 24),
                EndDate = new DateOnly(2026, 8, 25),
                Period = "day",
            },
            RecipientUserIds = [Guid.NewGuid()],
        };

    private sealed class FailingDistributionService(Exception exception) : IScheduleDistributionService
    {
        public Task<ScheduleDistributionResult> DistributeAsync(
            ScheduleDistributionRequest request,
            CancellationToken cancellationToken = default
        ) => Task.FromException<ScheduleDistributionResult>(exception);
    }

    private sealed class RecordingDistributionService : IScheduleDistributionService
    {
        public int CallCount { get; private set; }

        public Task<ScheduleDistributionResult> DistributeAsync(
            ScheduleDistributionRequest request,
            CancellationToken cancellationToken = default
        )
        {
            CallCount++;
            return Task.FromResult(new ScheduleDistributionResult(1));
        }
    }
}
