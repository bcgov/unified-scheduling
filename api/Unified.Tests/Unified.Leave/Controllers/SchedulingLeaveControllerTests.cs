using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unified.Calendar.Services;
using Unified.Leave;
using Unified.Leave.Controllers;
using Unified.Leave.Models.Scheduling;
using Unified.Leave.Services;
using Unified.Leave.Validators;

namespace Unified.Tests.Leave.Controllers;

public sealed class SchedulingLeaveControllerTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");

    private readonly FakeSchedulingLeaveService _service = new();
    private readonly SchedulingLeaveController _controller;

    public SchedulingLeaveControllerTests()
    {
        _controller = new SchedulingLeaveController(
            _service,
            new SchedulingLeaveRequestValidator(),
            new SchedulingLeaveSeriesRequestValidator(new AlwaysValidRecurrenceRuleValidator())
        );
    }

    [Fact]
    public async Task CreateLeaveEntry_WhenValid_ReturnsCreatedWithLocation()
    {
        _service.Entry = CreateEntryResponse(11);

        var result = await _controller.CreateLeaveEntry(EntryRequest(), TestContext.Current.CancellationToken);

        var created = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal("/api/scheduling/leave/entries/11", created.Location);
        Assert.Same(_service.Entry, created.Value);
    }

    [Fact]
    public async Task CreateLeaveEntry_WhenInvalid_ThrowsValidationExceptionWithoutCallingService()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _controller.CreateLeaveEntry(EntryRequest() with { UserIds = [] }, TestContext.Current.CancellationToken)
        );

        Assert.Equal(0, _service.WriteCalls);
    }

    [Fact]
    public async Task GetLeaveEntryById_WhenFoundOrMissing_ReturnsOkOrNotFound()
    {
        var missing = await _controller.GetLeaveEntryById(1, TestContext.Current.CancellationToken);
        _service.Entry = CreateEntryResponse(1);
        var found = await _controller.GetLeaveEntryById(1, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(missing.Result);
        Assert.Same(_service.Entry, Assert.IsType<OkObjectResult>(found.Result).Value);
    }

    [Fact]
    public async Task UpdateLeaveEntry_WhenFoundOrMissing_ReturnsOkOrNotFound()
    {
        var missing = await _controller.UpdateLeaveEntry(1, EntryRequest(), TestContext.Current.CancellationToken);
        _service.Entry = CreateEntryResponse(1);
        var found = await _controller.UpdateLeaveEntry(1, EntryRequest(), TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(missing.Result);
        Assert.Same(_service.Entry, Assert.IsType<OkObjectResult>(found.Result).Value);
    }

    [Fact]
    public async Task UpdateLeaveEntry_WhenInvalid_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _controller.UpdateLeaveEntry(
                1,
                EntryRequest() with
                {
                    LeaveTypeId = 0,
                },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(0, _service.WriteCalls);
    }

    [Fact]
    public async Task DeleteLeaveEntry_ReturnsNoContentOrNotFound()
    {
        var missing = await _controller.DeleteLeaveEntry(1, TestContext.Current.CancellationToken);
        _service.DeleteResult = true;
        var deleted = await _controller.DeleteLeaveEntry(1, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(missing);
        Assert.IsType<NoContentResult>(deleted);
    }

    [Fact]
    public async Task CreateLeaveSeries_WhenValid_ReturnsCreatedWithLocation()
    {
        _service.Series = CreateSeriesResponse(21);

        var result = await _controller.CreateLeaveSeries(SeriesRequest(), TestContext.Current.CancellationToken);

        var created = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal("/api/scheduling/leave/series/21", created.Location);
        Assert.Same(_service.Series, created.Value);
    }

    [Fact]
    public async Task CreateLeaveSeries_WhenInvalid_ThrowsValidationExceptionWithoutCallingService()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _controller.CreateLeaveSeries(
                SeriesRequest() with
                {
                    EndAtUtc = null,
                },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(0, _service.WriteCalls);
    }

    [Fact]
    public async Task GetLeaveSeriesById_WhenFoundOrMissing_ReturnsOkOrNotFound()
    {
        var missing = await _controller.GetLeaveSeriesById(1, TestContext.Current.CancellationToken);
        _service.Series = CreateSeriesResponse(1);
        var found = await _controller.GetLeaveSeriesById(1, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(missing.Result);
        Assert.Same(_service.Series, Assert.IsType<OkObjectResult>(found.Result).Value);
    }

    [Fact]
    public async Task UpdateLeaveSeries_WhenFoundOrMissing_ReturnsOkOrNotFound()
    {
        var missing = await _controller.UpdateLeaveSeries(1, SeriesRequest(), TestContext.Current.CancellationToken);
        _service.Series = CreateSeriesResponse(1);
        var found = await _controller.UpdateLeaveSeries(1, SeriesRequest(), TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(missing.Result);
        Assert.Same(_service.Series, Assert.IsType<OkObjectResult>(found.Result).Value);
    }

    [Fact]
    public async Task DeleteLeaveSeries_ReturnsNoContentOrNotFound()
    {
        var missing = await _controller.DeleteLeaveSeries(1, TestContext.Current.CancellationToken);
        _service.DeleteResult = true;
        var deleted = await _controller.DeleteLeaveSeries(1, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(missing);
        Assert.IsType<NoContentResult>(deleted);
    }

    [Fact]
    public async Task GetByUser_ReturnsOkWithServiceResults()
    {
        var entries = await _controller.GetLeaveEntriesByUser(UserA, TestContext.Current.CancellationToken);
        var series = await _controller.GetLeaveSeriesByUser(UserA, TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(entries.Result);
        Assert.IsType<OkObjectResult>(series.Result);
    }

    [Theory]
    [InlineData(nameof(SchedulingLeaveController.GetLeaveEntryById), LeavePolicies.LeaveViewOrUser)]
    [InlineData(nameof(SchedulingLeaveController.CreateLeaveEntry), LeavePolicies.LeaveCreateOrUser)]
    [InlineData(nameof(SchedulingLeaveController.UpdateLeaveEntry), LeavePolicies.LeaveEditOrUser)]
    [InlineData(nameof(SchedulingLeaveController.DeleteLeaveEntry), LeavePolicies.LeaveDeleteOrUser)]
    [InlineData(nameof(SchedulingLeaveController.CreateLeaveSeries), LeavePolicies.LeaveCreateOrUser)]
    [InlineData(nameof(SchedulingLeaveController.UpdateLeaveSeries), LeavePolicies.LeaveEditOrUser)]
    [InlineData(nameof(SchedulingLeaveController.DeleteLeaveSeries), LeavePolicies.LeaveDeleteOrUser)]
    public void Actions_RequireExpectedAuthorizationPolicy(string actionName, string expectedPolicy)
    {
        var method = typeof(SchedulingLeaveController).GetMethod(actionName)!;

        var attribute = Assert.Single(method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false));

        Assert.Equal(expectedPolicy, ((AuthorizeAttribute)attribute).Policy);
    }

    private static SchedulingLeaveEntryRequest EntryRequest() =>
        new()
        {
            Title = "Leave",
            StartAtUtc = Start,
            EndAtUtc = Start.AddHours(8),
            TimeZoneId = "America/Vancouver",
            LocationId = 5,
            LeaveTypeId = 1,
            UserIds = [UserA],
        };

    private static SchedulingLeaveSeriesRequest SeriesRequest() =>
        new()
        {
            Title = "Series",
            RecurrenceRule = "FREQ=DAILY;COUNT=3",
            StartAtUtc = Start,
            EndAtUtc = Start.AddHours(8),
            TimeZoneId = "America/Vancouver",
            LocationId = 5,
            LeaveTypeId = 1,
            UserIds = [UserA],
        };

    private static SchedulingLeaveEntryResponse CreateEntryResponse(int id) =>
        new()
        {
            Id = id,
            Title = "Leave",
            StartAtUtc = Start,
        };

    private static SchedulingLeaveSeriesResponse CreateSeriesResponse(int id) =>
        new()
        {
            Id = id,
            Title = "Series",
            RecurrenceRule = "FREQ=DAILY;COUNT=3",
            StartAtUtc = Start,
        };

    private sealed class AlwaysValidRecurrenceRuleValidator : IRecurrenceRuleValidator
    {
        public RecurrenceValidationResult Validate(
            string recurrenceRule,
            DateTimeOffset seriesStartAtUtc,
            DateTimeOffset? seriesEndAtUtc,
            string? timeZoneId,
            RecurrenceValidationOptions options
        ) => RecurrenceValidationResult.Success;
    }

    private sealed class FakeSchedulingLeaveService : ISchedulingLeaveService
    {
        public SchedulingLeaveEntryResponse? Entry { get; set; }

        public SchedulingLeaveSeriesResponse? Series { get; set; }

        public bool DeleteResult { get; set; }

        public int WriteCalls { get; private set; }

        public Task<SchedulingLeaveEntryResponse?> GetLeaveEntryByIdAsync(int id, CancellationToken ct) =>
            Task.FromResult(Entry);

        public Task<IReadOnlyList<SchedulingLeaveEntryResponse>> GetLeaveEntriesByUserAsync(
            Guid userId,
            CancellationToken ct
        ) => Task.FromResult<IReadOnlyList<SchedulingLeaveEntryResponse>>([]);

        public Task<IReadOnlyList<SchedulingLeaveSeriesResponse>> GetLeaveSeriesByUserAsync(
            Guid userId,
            CancellationToken ct
        ) => Task.FromResult<IReadOnlyList<SchedulingLeaveSeriesResponse>>([]);

        public Task<SchedulingLeaveEntryResponse> CreateLeaveEntryAsync(
            SchedulingLeaveEntryRequest request,
            CancellationToken ct
        )
        {
            WriteCalls++;
            return Task.FromResult(Entry!);
        }

        public Task<SchedulingLeaveEntryResponse?> UpdateLeaveEntryAsync(
            int id,
            SchedulingLeaveEntryRequest request,
            CancellationToken ct
        )
        {
            WriteCalls++;
            return Task.FromResult(Entry);
        }

        public Task<bool> DeleteLeaveEntryAsync(int id, CancellationToken ct)
        {
            WriteCalls++;
            return Task.FromResult(DeleteResult);
        }

        public Task<SchedulingLeaveSeriesResponse?> GetLeaveSeriesByIdAsync(int id, CancellationToken ct) =>
            Task.FromResult(Series);

        public Task<SchedulingLeaveSeriesResponse> CreateLeaveSeriesAsync(
            SchedulingLeaveSeriesRequest request,
            CancellationToken ct
        )
        {
            WriteCalls++;
            return Task.FromResult(Series!);
        }

        public Task<SchedulingLeaveSeriesResponse?> UpdateLeaveSeriesAsync(
            int id,
            SchedulingLeaveSeriesRequest request,
            CancellationToken ct
        )
        {
            WriteCalls++;
            return Task.FromResult(Series);
        }

        public Task<bool> DeleteLeaveSeriesAsync(int id, CancellationToken ct)
        {
            WriteCalls++;
            return Task.FromResult(DeleteResult);
        }
    }
}
