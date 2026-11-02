using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unified.Leave;
using Unified.Leave.Controllers;
using Unified.Leave.Models.Calendar;
using Unified.Leave.Services;
using Unified.Leave.Validators;

namespace Unified.Tests.Leave.Controllers;

public sealed class LeaveCalendarControllerTests
{
    private static readonly Guid UserA = Guid.NewGuid();
    private static readonly Guid UserB = Guid.NewGuid();

    private static CalendarEventQueryContext Request() =>
        new()
        {
            StartAtUtc = new DateTimeOffset(2026, 11, 1, 7, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 12, 1, 7, 0, 0, TimeSpan.Zero),
            UserIds = [UserA],
        };

    [Fact]
    public async Task CalendarQuery_AcceptsReportedOctoberWeekPayload()
    {
        const string payload = """
            {"startAtUtc":"2026-10-26T07:00:00.000Z","endAtUtc":"2026-11-02T07:00:00.000Z","userIds":["e56808e2-a11d-4175-9ebf-f080e248be73","ea4ccb81-3dce-4f3d-8185-c9b4e50d1234"]}
            """;
        var request = JsonSerializer.Deserialize<CalendarEventQueryContext>(
            payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
        );
        Assert.NotNull(request);
        var validation = await new LeaveCalendarQueryValidator().ValidateAsync(
            request,
            TestContext.Current.CancellationToken
        );
        Assert.True(validation.IsValid, validation.ToString());
        Assert.Equal(2, request.UserIds.Count);
        Assert.Null(request.LocationId);
    }

    [Fact]
    public async Task GetData_DelegatesCrossLocationQueryAndPreservesLeaveIdentityForSelectedUsers()
    {
        var provider = new Provider();
        var controller = new LeaveCalendarController(provider, new LeaveCalendarQueryValidator());
        var request = Request();
        var response = await controller.GetData(request, TestContext.Current.CancellationToken);
        var events = Assert.IsAssignableFrom<IReadOnlyCollection<LeaveCalendarEvent>>(
            Assert.IsType<OkObjectResult>(response.Result).Value
        );
        var item = Assert.Single(events);
        Assert.Same(request, provider.Request);
        Assert.Null(provider.Request!.LocationId);
        Assert.Equal(42, item.LeaveEntryId);
        Assert.Equal(7, item.LeaveSeriesId);
        Assert.Equal(5, item.LocationId);
        Assert.Equal([UserA], item.UserIds);
        Assert.Equal([UserA, UserB], Assert.Single(provider.Events).UserIds);
    }

    [Theory]
    [InlineData("empty-users")]
    [InlineData("empty-user-id")]
    [InlineData("reversed")]
    [InlineData("too-long")]
    [InlineData("location")]
    public async Task GetData_InvalidQueryDoesNotCallProvider(string scenario)
    {
        var provider = new Provider();
        var request = Request();
        request = scenario switch
        {
            "empty-users" => request with { UserIds = [] },
            "empty-user-id" => request with { UserIds = [Guid.Empty] },
            "reversed" => request with { EndAtUtc = request.StartAtUtc },
            "too-long" => request with { EndAtUtc = request.StartAtUtc.AddDays(368) },
            _ => request with { LocationId = 0 },
        };
        var controller = new LeaveCalendarController(provider, new LeaveCalendarQueryValidator());
        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.GetData(request, TestContext.Current.CancellationToken)
        );
        Assert.Null(provider.Request);
    }

    [Fact]
    public async Task CalendarEndpointRequiresRegisteredLeaveViewPermission()
    {
        var attribute = Assert.Single(
            typeof(LeaveCalendarController)
                .GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>()
        );
        Assert.Equal(LeavePolicies.LeaveView, attribute.Policy);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLeaveModule(
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["FeatureFlags:Leave:Enabled"] = "true",
                        ["FeatureFlags:Calendar:Enabled"] = "true",
                    }
                )
                .Build()
        );
        await using var provider = services.BuildServiceProvider();
        var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await AuthorizationPolicy.CombineAsync(policies, [attribute]);
        Assert.NotNull(policy);
        Assert.NotEmpty(policy.Requirements);
    }

    private sealed class Provider : ILeaveCalendarDataProvider
    {
        public CalendarEventQueryContext? Request { get; private set; }
        public IReadOnlyCollection<LeaveCalendarEvent> Events { get; } =
        [
            new()
            {
                Id = "scheduling.leave-entry.42",
                LeaveEntryId = 42,
                LeaveSeriesId = 7,
                LocationId = 5,
                Title = "Leave",
                Type = "scheduling.leave",
                SourceModule = "leave",
                EventTypeCode = "leave",
                StatusTypeCode = "Draft",
                Start = RequestStart,
                End = RequestStart.AddHours(8),
                UserIds = [UserA, UserB],
            },
        ];
        private static DateTimeOffset RequestStart => new(2026, 11, 3, 7, 0, 0, TimeSpan.Zero);

        public Task<IReadOnlyCollection<LeaveCalendarEvent>> GetEventsAsync(
            CalendarEventQueryContext queryContext,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            Request = queryContext;
            return Task.FromResult(Events);
        }
    }
}
