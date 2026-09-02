using Microsoft.Extensions.Logging.Abstractions;
using Unified.Common.Reporting;
using Unified.Reporting.Models;
using Unified.Reporting.Services.Reporting;

namespace Unified.Tests.Reporting.Services;

public class ReportQueryServiceTests
{
    [Fact]
    public async Task ExecuteAsync_Should_Forward_Request_Values_And_Return_Handler_Response()
    {
        // Arrange
        var handler = new FakeReportQueryHandler("user-training", new FakePagedResponse(42));
        var service = new ReportQueryService([handler], NullLogger<ReportQueryService>.Instance);

        var request = new ReportQueryRequest(
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["userId"] = ["123"],
            },
            SortBy: "userDisplayName",
            SortDirection: SortDirection.Desc
        );

        // Act
        var result = await service.ExecuteAsync("user-training", request, TestContext.Current.CancellationToken);

        // Assert
        var typedResult = Assert.IsType<FakePagedResponse>(result);
        Assert.Equal(42, typedResult.TotalRows);

        Assert.Equal("userDisplayName", handler.LastSortBy);
        Assert.Equal(SortDirection.Desc, handler.LastSortDirection);
        Assert.True(handler.LastFilters.ContainsKey("userId"));
    }

    [Fact]
    public async Task ExecuteAsync_Should_Throw_When_Report_Not_Registered()
    {
        // Arrange
        var service = new ReportQueryService([], NullLogger<ReportQueryService>.Instance);
        var request = new ReportQueryRequest(new Dictionary<string, IReadOnlyCollection<string>>());

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ExecuteAsync("missing", request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public void Constructor_Should_Throw_For_Duplicate_Report_Keys()
    {
        // Arrange
        var handlers = new IReportQueryHandler[]
        {
            new FakeReportQueryHandler("dup", new FakePagedResponse(0)),
            new FakeReportQueryHandler(" DUP ", new FakePagedResponse(0)),
        };

        // Act + Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ReportQueryService(handlers, NullLogger<ReportQueryService>.Instance)
        );

        Assert.Contains("Duplicate report handler registration", ex.Message);
    }

    private sealed class FakeReportQueryHandler(string reportKey, PagedResponse response) : IReportQueryHandler
    {
        public string ReportKey => reportKey;

        public IReadOnlyDictionary<string, IReadOnlyCollection<string>> LastFilters { get; private set; } =
            new Dictionary<string, IReadOnlyCollection<string>>();

        public string? LastSortBy { get; private set; }

        public SortDirection LastSortDirection { get; private set; }

        public Task<PagedResponse> ExecuteAsync(
            IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters,
            string? sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken = default
        )
        {
            LastFilters = filters;
            LastSortBy = sortBy;
            LastSortDirection = sortDirection;

            return Task.FromResult(response);
        }
    }

    private sealed record FakePagedResponse(int TotalRows) : PagedResponse(TotalRows);
}
