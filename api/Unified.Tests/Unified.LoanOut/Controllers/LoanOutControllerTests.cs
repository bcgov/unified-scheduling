using Microsoft.AspNetCore.Mvc;
using Unified.Calendar.Services;
using Unified.Db.Models.LoanOut;
using Unified.LoanOut.Controllers;
using Unified.LoanOut.Models;
using Unified.LoanOut.Services;
using Unified.LoanOut.Validators;

namespace Unified.Tests.LoanOut.Controllers;

public sealed class LoanOutControllerTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-06-01T16:00:00Z");

    [Fact]
    public async Task CreateLoanOutEntry_ReturnsCreatedLocationAndResponse()
    {
        var service = new FakeLoanOutService();
        var controller = CreateController(service);
        var request = CreateEntryRequest();
        var response = new LoanOutEntryResponse
        {
            Id = 12,
            Title = request.Title,
            StartAtUtc = request.StartAtUtc,
        };
        service.EntryResult = response;

        var result = await controller.CreateLoanOutEntry(request, TestContext.Current.CancellationToken);

        var created = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal("/api/scheduling/loan-out/entries/12", created.Location);
        Assert.Same(response, created.Value);
        Assert.Same(request, service.LastEntryRequest);
    }

    [Fact]
    public async Task CreateLoanOutSeries_ReturnsCreatedLocationAndResponse()
    {
        var service = new FakeLoanOutService();
        var controller = CreateController(service);
        var request = CreateSeriesRequest();
        var response = new LoanOutSeriesResponse
        {
            Id = 14,
            Title = request.Title,
            StartAtUtc = request.StartAtUtc,
        };
        service.SeriesResult = response;

        var result = await controller.CreateLoanOutSeries(request, TestContext.Current.CancellationToken);

        var created = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal("/api/scheduling/loan-out/series/14", created.Location);
        Assert.Same(response, created.Value);
        Assert.Same(request, service.LastSeriesRequest);
    }

    [Fact]
    public async Task GetLoanOutEntryById_WhenMissing_ReturnsNotFound()
    {
        var controller = CreateController(new FakeLoanOutService { EntryResult = null });

        var result = await controller.GetLoanOutEntryById(9, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateLoanOutSeries_WhenMissing_ReturnsNotFound()
    {
        var controller = CreateController(new FakeLoanOutService { SeriesResult = null });

        var result = await controller.UpdateLoanOutSeries(
            9,
            CreateSeriesRequest(),
            TestContext.Current.CancellationToken
        );

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task DeleteLoanOutEntry_WhenDeleted_ReturnsNoContent()
    {
        var result = await CreateController(new FakeLoanOutService { DeleteEntryResult = true })
            .DeleteLoanOutEntry(9, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
    }

    private static LoanOutController CreateController(FakeLoanOutService service) =>
        new(service, new LoanOutRequestValidator(), new LoanOutSeriesRequestValidator(new FakeRecurrenceValidator()));

    private static LoanOutEntryRequest CreateEntryRequest() =>
        new()
        {
            Title = "Loan out",
            StartAtUtc = Start,
            EndAtUtc = Start.AddHours(4),
            TimeZoneId = "America/Vancouver",
            LocationId = 5,
            LoanOutLocationId = 10,
            UserIds = [Guid.NewGuid()],
        };

    private static LoanOutSeriesRequest CreateSeriesRequest() =>
        new()
        {
            Title = "Loan out series",
            RecurrenceRule = "FREQ=DAILY;COUNT=2",
            StartAtUtc = Start,
            EndAtUtc = Start.AddHours(4),
            TimeZoneId = "America/Vancouver",
            LocationId = 5,
            LoanOutLocationId = 10,
            UserIds = [Guid.NewGuid()],
        };

    private sealed class FakeRecurrenceValidator : IRecurrenceRuleValidator
    {
        public RecurrenceValidationResult Validate(
            string recurrenceRule,
            DateTimeOffset seriesStartAtUtc,
            DateTimeOffset? seriesEndAtUtc,
            string? timeZoneId,
            RecurrenceValidationOptions options
        ) => RecurrenceValidationResult.Success;
    }

    private sealed class FakeLoanOutService : ILoanOutService
    {
        public LoanOutEntryResponse? EntryResult { get; set; } =
            new()
            {
                Id = 1,
                Title = "",
                StartAtUtc = default,
            };
        public LoanOutSeriesResponse? SeriesResult { get; set; } =
            new()
            {
                Id = 1,
                Title = "",
                StartAtUtc = default,
            };
        public bool DeleteEntryResult { get; set; }
        public LoanOutEntryRequest? LastEntryRequest { get; private set; }
        public LoanOutSeriesRequest? LastSeriesRequest { get; private set; }

        public Task<LoanOutEntryResponse?> GetLoanOutEntryByIdAsync(
            int id,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(EntryResult);

        public Task<IReadOnlyList<LoanOutEntryResponse>> GetLoanOutEntriesByUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<LoanOutEntryResponse>>([]);

        public Task<LoanOutEntryResponse> CreateLoanOutEntryAsync(
            LoanOutEntryRequest request,
            CancellationToken cancellationToken = default
        )
        {
            LastEntryRequest = request;
            return Task.FromResult(EntryResult!);
        }

        public Task<LoanOutEntryResponse?> UpdateLoanOutEntryAsync(
            int id,
            LoanOutEntryRequest request,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(EntryResult);

        public Task<bool> DeleteLoanOutEntryAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(DeleteEntryResult);

        public Task TransferRelatedShiftsAsync(
            LoanOutEntry entity,
            bool deleteAssignments = false,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task<IReadOnlyList<LoanOutSeriesResponse>> GetLoanOutSeriesByUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<LoanOutSeriesResponse>>([]);

        public Task<LoanOutSeriesResponse?> GetLoanOutSeriesByIdAsync(
            int id,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(SeriesResult);

        public Task<LoanOutSeriesResponse> CreateLoanOutSeriesAsync(
            LoanOutSeriesRequest request,
            CancellationToken cancellationToken = default
        )
        {
            LastSeriesRequest = request;
            return Task.FromResult(SeriesResult!);
        }

        public Task<LoanOutSeriesResponse?> UpdateLoanOutSeriesAsync(
            int id,
            LoanOutSeriesRequest request,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(SeriesResult);

        public Task<bool> DeleteLoanOutSeriesAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task TransferRelatedShiftsAsync(
            LoanOutSeries entity,
            bool deleteAssignments = false,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }
}
