using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Scheduling;
using Unified.Scheduling.Controllers;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;
using Unified.Scheduling.Validators;

namespace Unified.Tests.Scheduling.Controllers;

public sealed class SchedulingAuthorizationTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SchedulingCalendar_GetData_ForwardsGrantedViewPermissions(bool shiftsView, bool assignmentsView)
    {
        var service = new RecordingSchedulingCalendarService();
        var controller = CreateCalendarController(service, shiftsView, assignmentsView);

        var result = await controller.GetData(CreateCalendarRequest(), TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(shiftsView, service.IncludeShifts);
        Assert.Equal(assignmentsView, service.IncludeAssignments);
        Assert.Equal(1, service.CallCount);
    }

    [Fact]
    public async Task SchedulingCalendar_GetData_WithoutEitherViewPermission_ReturnsForbidden()
    {
        var service = new RecordingSchedulingCalendarService();
        var controller = CreateCalendarController(service, shiftsView: false, assignmentsView: false);

        var result = await controller.GetData(CreateCalendarRequest(), TestContext.Current.CancellationToken);

        Assert.IsType<ForbidResult>(result.Result);
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public void ShiftAssignmentController_AllMutationActions_RequireAssignmentsAssign()
    {
        var mutationMethods = typeof(ShiftAssignmentController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method =>
                method
                    .GetCustomAttributes()
                    .Any(attribute => attribute is HttpPostAttribute or HttpPutAttribute or HttpDeleteAttribute)
            )
            .ToList();

        Assert.Equal(6, mutationMethods.Count);
        Assert.All(
            mutationMethods,
            method =>
            {
                var authorize = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());
                Assert.Equal(SchedulingPolicies.AssignmentsAssign, authorize.Policy);
            }
        );
    }

    private static SchedulingCalendarController CreateCalendarController(
        RecordingSchedulingCalendarService service,
        bool shiftsView,
        bool assignmentsView
    )
    {
        var claims = new List<Claim>();
        if (shiftsView)
            claims.Add(new Claim(UnifiedClaimTypes.Permission, Permissions.ShiftsView.ToString()));
        if (assignmentsView)
            claims.Add(new Claim(UnifiedClaimTypes.Permission, Permissions.AssignmentsView.ToString()));

        return new SchedulingCalendarController(service, new SchedulingCalendarRequestValidator())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) },
            },
        };
    }

    private static SchedulingCalendarRequest CreateCalendarRequest() =>
        new()
        {
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 1),
            TimeZoneId = "UTC",
        };

    private sealed class RecordingSchedulingCalendarService : ISchedulingCalendarService
    {
        public int CallCount { get; private set; }
        public bool IncludeShifts { get; private set; }
        public bool IncludeAssignments { get; private set; }

        public Task<SchedulingCalendarDataResponse> GetDataAsync(
            SchedulingCalendarRequest request,
            bool includeShifts,
            bool includeAssignments,
            CancellationToken cancellationToken = default
        )
        {
            CallCount++;
            IncludeShifts = includeShifts;
            IncludeAssignments = includeAssignments;
            return Task.FromResult(new SchedulingCalendarDataResponse());
        }
    }
}
