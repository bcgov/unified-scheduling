using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unified.Calendar.Services;
using Unified.TimeOff;
using Unified.TimeOff.Controllers;
using Unified.TimeOff.Models.Scheduling;
using Unified.TimeOff.Services;
using Unified.TimeOff.Validators;

namespace Unified.Tests.TimeOff.Controllers;

public sealed class SchedulingTimeOffControllerTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 16, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserA = new("11111111-1111-1111-1111-111111111111");

    private readonly FakeSchedulingTimeOffService _service = new();
    private readonly SchedulingTimeOffController _controller;

    public SchedulingTimeOffControllerTests()
    {
        _controller = new SchedulingTimeOffController(
            _service,
            new SchedulingTimeOffRequestValidator(),
            new SchedulingTimeOffSeriesRequestValidator(new AlwaysValidRecurrenceRuleValidator())
        );
    }

    [Fact]
    public async Task CreateTimeOffEntry_WhenValid_ReturnsCreatedWithLocation()
    {
        _service.Entry = new SchedulingTimeOffEntryResponse { Id = 11 };

        var result = await _controller.CreateTimeOffEntry(EntryRequest(), TestContext.Current.CancellationToken);

        var created = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal("/api/scheduling/timeoff/entries/11", created.Location);
        Assert.Same(_service.Entry, created.Value);
    }

    [Fact]
    public async Task CreateTimeOffEntry_WhenInvalid_ThrowsValidationExceptionWithoutCallingService()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _controller.CreateTimeOffEntry(EntryRequest() with { UserIds = [] }, TestContext.Current.CancellationToken)
        );

        Assert.Equal(0, _service.WriteCalls);
    }

    [Fact]
    public async Task GetTimeOffEntryById_WhenFoundOrMissing_ReturnsOkOrNotFound()
    {
        var missing = await _controller.GetTimeOffEntryById(1, TestContext.Current.CancellationToken);
        _service.Entry = new SchedulingTimeOffEntryResponse { Id = 1 };
        var found = await _controller.GetTimeOffEntryById(1, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(missing.Result);
        Assert.Same(_service.Entry, Assert.IsType<OkObjectResult>(found.Result).Value);
    }

    [Fact]
    public async Task UpdateTimeOffEntry_WhenFoundOrMissing_ReturnsOkOrNotFound()
    {
        var missing = await _controller.UpdateTimeOffEntry(1, EntryRequest(), TestContext.Current.CancellationToken);
        _service.Entry = new SchedulingTimeOffEntryResponse { Id = 1 };
        var found = await _controller.UpdateTimeOffEntry(1, EntryRequest(), TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(missing.Result);
        Assert.Same(_service.Entry, Assert.IsType<OkObjectResult>(found.Result).Value);
    }

    [Fact]
    public async Task UpdateTimeOffEntry_WhenInvalid_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _controller.UpdateTimeOffEntry(
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
    public async Task DeleteTimeOffEntry_ReturnsNoContentOrNotFound()
    {
        var missing = await _controller.DeleteTimeOffEntry(1, TestContext.Current.CancellationToken);
        _service.DeleteResult = true;
        var deleted = await _controller.DeleteTimeOffEntry(1, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(missing);
        Assert.IsType<NoContentResult>(deleted);
    }

    [Fact]
    public async Task CreateTimeOffSeries_WhenValid_ReturnsCreatedWithLocation()
    {
        _service.Series = new SchedulingTimeOffSeriesResponse { Id = 21 };

        var result = await _controller.CreateTimeOffSeries(SeriesRequest(), TestContext.Current.CancellationToken);

        var created = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal("/api/scheduling/timeoff/series/21", created.Location);
        Assert.Same(_service.Series, created.Value);
    }

    [Fact]
    public async Task CreateTimeOffSeries_WhenInvalid_ThrowsValidationExceptionWithoutCallingService()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _controller.CreateTimeOffSeries(
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
    public async Task GetTimeOffSeriesById_WhenFoundOrMissing_ReturnsOkOrNotFound()
    {
        var missing = await _controller.GetTimeOffSeriesById(1, TestContext.Current.CancellationToken);
        _service.Series = new SchedulingTimeOffSeriesResponse { Id = 1 };
        var found = await _controller.GetTimeOffSeriesById(1, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(missing.Result);
        Assert.Same(_service.Series, Assert.IsType<OkObjectResult>(found.Result).Value);
    }

    [Fact]
    public async Task UpdateTimeOffSeries_WhenFoundOrMissing_ReturnsOkOrNotFound()
    {
        var missing = await _controller.UpdateTimeOffSeries(1, SeriesRequest(), TestContext.Current.CancellationToken);
        _service.Series = new SchedulingTimeOffSeriesResponse { Id = 1 };
        var found = await _controller.UpdateTimeOffSeries(1, SeriesRequest(), TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(missing.Result);
        Assert.Same(_service.Series, Assert.IsType<OkObjectResult>(found.Result).Value);
    }

    [Fact]
    public async Task DeleteTimeOffSeries_ReturnsNoContentOrNotFound()
    {
        var missing = await _controller.DeleteTimeOffSeries(1, TestContext.Current.CancellationToken);
        _service.DeleteResult = true;
        var deleted = await _controller.DeleteTimeOffSeries(1, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(missing);
        Assert.IsType<NoContentResult>(deleted);
    }

    [Fact]
    public async Task GetByUser_ReturnsOkWithServiceResults()
    {
        var entries = await _controller.GetTimeOffEntriesByUser(UserA, TestContext.Current.CancellationToken);
        var series = await _controller.GetTimeOffSeriesByUser(UserA, TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(entries.Result);
        Assert.IsType<OkObjectResult>(series.Result);
    }

    [Theory]
    [InlineData(nameof(SchedulingTimeOffController.GetTimeOffEntryById), TimeOffPolicies.TimeOffViewOrUser)]
    [InlineData(nameof(SchedulingTimeOffController.CreateTimeOffEntry), TimeOffPolicies.TimeOffCreateOrUser)]
    [InlineData(nameof(SchedulingTimeOffController.UpdateTimeOffEntry), TimeOffPolicies.TimeOffEditOrUser)]
    [InlineData(nameof(SchedulingTimeOffController.DeleteTimeOffEntry), TimeOffPolicies.TimeOffDeleteOrUser)]
    [InlineData(nameof(SchedulingTimeOffController.CreateTimeOffSeries), TimeOffPolicies.TimeOffCreateOrUser)]
    [InlineData(nameof(SchedulingTimeOffController.UpdateTimeOffSeries), TimeOffPolicies.TimeOffEditOrUser)]
    [InlineData(nameof(SchedulingTimeOffController.DeleteTimeOffSeries), TimeOffPolicies.TimeOffDeleteOrUser)]
    public void Actions_RequireExpectedAuthorizationPolicy(string actionName, string expectedPolicy)
    {
        var method = typeof(SchedulingTimeOffController).GetMethod(actionName)!;

        var attribute = Assert.Single(method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false));

        Assert.Equal(expectedPolicy, ((AuthorizeAttribute)attribute).Policy);
    }

    private static SchedulingTimeOffEntryRequest EntryRequest() =>
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

    private static SchedulingTimeOffSeriesRequest SeriesRequest() =>
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

    private sealed class FakeSchedulingTimeOffService : ISchedulingTimeOffService
    {
        public SchedulingTimeOffEntryResponse? Entry { get; set; }

        public SchedulingTimeOffSeriesResponse? Series { get; set; }

        public bool DeleteResult { get; set; }

        public int WriteCalls { get; private set; }

        public Task<SchedulingTimeOffEntryResponse?> GetTimeOffEntryByIdAsync(int id, CancellationToken ct) =>
            Task.FromResult(Entry);

        public Task<IReadOnlyList<SchedulingTimeOffEntryResponse>> GetTimeOffEntriesByUserAsync(
            Guid userId,
            CancellationToken ct
        ) => Task.FromResult<IReadOnlyList<SchedulingTimeOffEntryResponse>>([]);

        public Task<IReadOnlyList<SchedulingTimeOffSeriesResponse>> GetTimeOffSeriesByUserAsync(
            Guid userId,
            CancellationToken ct
        ) => Task.FromResult<IReadOnlyList<SchedulingTimeOffSeriesResponse>>([]);

        public Task<SchedulingTimeOffEntryResponse> CreateTimeOffEntryAsync(
            SchedulingTimeOffEntryRequest request,
            CancellationToken ct
        )
        {
            WriteCalls++;
            return Task.FromResult(Entry!);
        }

        public Task<SchedulingTimeOffEntryResponse?> UpdateTimeOffEntryAsync(
            int id,
            SchedulingTimeOffEntryRequest request,
            CancellationToken ct
        )
        {
            WriteCalls++;
            return Task.FromResult(Entry);
        }

        public Task<bool> DeleteTimeOffEntryAsync(int id, CancellationToken ct)
        {
            WriteCalls++;
            return Task.FromResult(DeleteResult);
        }

        public Task<SchedulingTimeOffSeriesResponse?> GetTimeOffSeriesByIdAsync(int id, CancellationToken ct) =>
            Task.FromResult(Series);

        public Task<SchedulingTimeOffSeriesResponse> CreateTimeOffSeriesAsync(
            SchedulingTimeOffSeriesRequest request,
            CancellationToken ct
        )
        {
            WriteCalls++;
            return Task.FromResult(Series!);
        }

        public Task<SchedulingTimeOffSeriesResponse?> UpdateTimeOffSeriesAsync(
            int id,
            SchedulingTimeOffSeriesRequest request,
            CancellationToken ct
        )
        {
            WriteCalls++;
            return Task.FromResult(Series);
        }

        public Task<bool> DeleteTimeOffSeriesAsync(int id, CancellationToken ct)
        {
            WriteCalls++;
            return Task.FromResult(DeleteResult);
        }
    }
}
