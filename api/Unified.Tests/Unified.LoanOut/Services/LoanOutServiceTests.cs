using Unified.Db.Models.LoanOut;
using Unified.LoanOut.Models;
using Unified.LoanOut.Services;

namespace Unified.Tests.LoanOut.Services;

public sealed class LoanOutServiceTests
{
    [Fact]
    public async Task EntryOperations_DelegateArgumentsAndResults()
    {
        var entryService = new FakeEntryService();
        var service = new LoanOutService(entryService, new FakeSeriesService());
        var token = new CancellationTokenSource().Token;
        var request = new LoanOutEntryRequest
        {
            Title = "Loan out",
            StartAtUtc = DateTimeOffset.Parse("2026-06-01T16:00:00Z"),
            LoanOutLocationId = 10,
            UserIds = [Guid.NewGuid()],
        };
        var expected = new LoanOutEntryResponse { Title = "Loan out", StartAtUtc = request.StartAtUtc };

        entryService.EntryResult = expected;
        Assert.Same(expected, await service.GetLoanOutEntryByIdAsync(12, token));
        Assert.Same(expected, await service.CreateLoanOutEntryAsync(request, token));
        Assert.Same(expected, await service.UpdateLoanOutEntryAsync(12, request, token));
        Assert.True(await service.DeleteLoanOutEntryAsync(12, token));

        Assert.Equal(12, entryService.LastId);
        Assert.Same(request, entryService.LastEntryRequest);
        Assert.Equal(token, entryService.LastToken);
    }

    [Fact]
    public async Task SeriesOperations_DelegateArgumentsAndResults()
    {
        var seriesService = new FakeSeriesService();
        var service = new LoanOutService(new FakeEntryService(), seriesService);
        var token = new CancellationTokenSource().Token;
        var request = new LoanOutSeriesRequest
        {
            Title = "Loan out series",
            RecurrenceRule = "FREQ=DAILY;COUNT=2",
            StartAtUtc = DateTimeOffset.Parse("2026-06-01T16:00:00Z"),
            EndAtUtc = DateTimeOffset.Parse("2026-06-01T17:00:00Z"),
            LoanOutLocationId = 10,
            UserIds = [Guid.NewGuid()],
        };
        var expected = new LoanOutSeriesResponse { Title = "Loan out series", StartAtUtc = request.StartAtUtc };

        seriesService.SeriesResult = expected;
        Assert.Same(expected, await service.GetLoanOutSeriesByIdAsync(13, token));
        Assert.Same(expected, await service.CreateLoanOutSeriesAsync(request, token));
        Assert.Same(expected, await service.UpdateLoanOutSeriesAsync(13, request, token));
        Assert.True(await service.DeleteLoanOutSeriesAsync(13, token));

        Assert.Equal(13, seriesService.LastId);
        Assert.Same(request, seriesService.LastSeriesRequest);
        Assert.Equal(token, seriesService.LastToken);
    }

    private sealed class FakeEntryService : ILoanOutEntryService
    {
        public LoanOutEntryResponse EntryResult { get; set; } = new() { Title = "", StartAtUtc = default };
        public int LastId { get; private set; }
        public LoanOutEntryRequest? LastEntryRequest { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public Task<LoanOutEntryResponse?> GetLoanOutEntryByIdAsync(
            int id,
            CancellationToken cancellationToken = default
        )
        {
            Record(id, cancellationToken);
            return Task.FromResult<LoanOutEntryResponse?>(EntryResult);
        }

        public Task<IReadOnlyList<LoanOutEntryResponse>> GetLoanOutEntriesByUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default
        )
        {
            LastToken = cancellationToken;
            return Task.FromResult<IReadOnlyList<LoanOutEntryResponse>>([EntryResult]);
        }

        public Task<LoanOutEntryResponse> CreateLoanOutEntryAsync(
            LoanOutEntryRequest request,
            CancellationToken cancellationToken = default
        )
        {
            LastEntryRequest = request;
            LastToken = cancellationToken;
            return Task.FromResult(EntryResult);
        }

        public Task<LoanOutEntryResponse?> UpdateLoanOutEntryAsync(
            int id,
            LoanOutEntryRequest request,
            CancellationToken cancellationToken = default
        )
        {
            Record(id, cancellationToken);
            LastEntryRequest = request;
            return Task.FromResult<LoanOutEntryResponse?>(EntryResult);
        }

        public Task<bool> DeleteLoanOutEntryAsync(int id, CancellationToken cancellationToken = default)
        {
            Record(id, cancellationToken);
            return Task.FromResult(true);
        }

        public Task TransferRelatedShiftsAsync(
            LoanOutEntry entity,
            bool deleteAssignments = false,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        private void Record(int id, CancellationToken token)
        {
            LastId = id;
            LastToken = token;
        }
    }

    private sealed class FakeSeriesService : ILoanOutSeriesService
    {
        public LoanOutSeriesResponse SeriesResult { get; set; } = new() { Title = "", StartAtUtc = default };
        public int LastId { get; private set; }
        public LoanOutSeriesRequest? LastSeriesRequest { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public Task<IReadOnlyList<LoanOutSeriesResponse>> GetLoanOutSeriesByUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default
        )
        {
            LastToken = cancellationToken;
            return Task.FromResult<IReadOnlyList<LoanOutSeriesResponse>>([SeriesResult]);
        }

        public Task<LoanOutSeriesResponse?> GetLoanOutSeriesByIdAsync(
            int id,
            CancellationToken cancellationToken = default
        )
        {
            Record(id, cancellationToken);
            return Task.FromResult<LoanOutSeriesResponse?>(SeriesResult);
        }

        public Task<LoanOutSeriesResponse> CreateLoanOutSeriesAsync(
            LoanOutSeriesRequest request,
            CancellationToken cancellationToken = default
        )
        {
            LastSeriesRequest = request;
            LastToken = cancellationToken;
            return Task.FromResult(SeriesResult);
        }

        public Task<LoanOutSeriesResponse?> UpdateLoanOutSeriesAsync(
            int id,
            LoanOutSeriesRequest request,
            CancellationToken cancellationToken = default
        )
        {
            Record(id, cancellationToken);
            LastSeriesRequest = request;
            return Task.FromResult<LoanOutSeriesResponse?>(SeriesResult);
        }

        public Task<bool> DeleteLoanOutSeriesAsync(int id, CancellationToken cancellationToken = default)
        {
            Record(id, cancellationToken);
            return Task.FromResult(true);
        }

        public Task TransferRelatedShiftsAsync(
            LoanOutSeries entity,
            bool deleteAssignments = false,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        private void Record(int id, CancellationToken token)
        {
            LastId = id;
            LastToken = token;
        }
    }
}
