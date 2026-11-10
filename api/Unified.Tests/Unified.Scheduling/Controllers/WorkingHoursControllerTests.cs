using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Unified.Infrastructure.ErrorHandling;
using Unified.Leave;
using Unified.Leave.FeatureFlags;
using Unified.Scheduling;
using Unified.Scheduling.Controllers;
using Unified.Scheduling.Models;
using Unified.Scheduling.Options;
using Unified.Scheduling.Services;
using Unified.Scheduling.Validators;

namespace Unified.Tests.Scheduling.Controllers;

public sealed class WorkingHoursControllerTests
{
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    public async Task Query_LeavePermissionAndFeatureFlag_EnforcesAuthorizationBeforeReading(
        bool leaveEnabled,
        bool canViewLeave,
        bool allowed
    )
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddAuthorizationBuilder()
            .AddPolicy(LeavePolicies.LeaveView, policy => policy.RequireClaim("permission", "LeaveView"));
        await using var provider = services.BuildServiceProvider();
        var service = new FakeWorkingHoursService();
        var user = new ClaimsPrincipal(
            new ClaimsIdentity(canViewLeave ? [new Claim("permission", "LeaveView")] : [], "test")
        );
        var controller = new WorkingHoursController(
            service,
            provider.GetRequiredService<IAuthorizationService>(),
            Options.Create(new LeaveFeatureFlags { Enabled = leaveEnabled }),
            new WorkingHoursQueryValidator(Options.Create(new WorkingHoursOptions()))
        )
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } },
        };

        var result = await controller.Query(
            new WorkingHoursQuery
            {
                StartDate = new(2026, 6, 1),
                EndDate = allowed ? new(2026, 6, 1) : new(2026, 5, 31),
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(allowed, service.Called);
        if (allowed)
            Assert.IsType<OkObjectResult>(result.Result);
        else
            Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task Query_InvalidRanges_UseExistingHttp400ValidationHandling()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddProblemDetails();
        await using var provider = services.BuildServiceProvider();
        var service = new FakeWorkingHoursService();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Response.Body = new MemoryStream();
        var controller = new WorkingHoursController(
            service,
            provider.GetRequiredService<IAuthorizationService>(),
            Options.Create(new LeaveFeatureFlags { Enabled = false }),
            new WorkingHoursQueryValidator(Options.Create(new WorkingHoursOptions { MaxQueryRangeDays = 31 }))
        )
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        };
        var start = new DateOnly(2026, 6, 1);
        foreach (var end in new[] { start.AddDays(-1), start.AddDays(31) })
        {
            var exception = await Assert.ThrowsAsync<ValidationException>(() =>
                controller.Query(
                    new WorkingHoursQuery { StartDate = start, EndDate = end },
                    TestContext.Current.CancellationToken
                )
            );
            var handler = new GlobalExceptionHandler(
                NullLogger<GlobalExceptionHandler>.Instance,
                provider.GetRequiredService<IProblemDetailsService>()
            );
            await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);
            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            Assert.False(service.Called);
        }
    }

    [Fact]
    public void Query_RetainsShiftAndAssignmentPermissionRequirements()
    {
        var policies = typeof(WorkingHoursController)
            .GetMethod(nameof(WorkingHoursController.Query))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(x => x.Policy)
            .ToArray();
        Assert.Contains(SchedulingPolicies.ShiftsView, policies);
        Assert.Contains(SchedulingPolicies.AssignmentsView, policies);
    }

    private sealed class FakeWorkingHoursService : IWorkingHoursService
    {
        public bool Called { get; private set; }

        public Task<IReadOnlyCollection<WorkingHoursResult>> QueryAsync(
            WorkingHoursQuery query,
            CancellationToken cancellationToken = default
        )
        {
            Called = true;
            return Task.FromResult<IReadOnlyCollection<WorkingHoursResult>>([]);
        }
    }
}
