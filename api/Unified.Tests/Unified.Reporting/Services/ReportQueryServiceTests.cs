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
        var handler = new FakeReportQueryHandler("user-training", new FakePagedResponse(2, 10, 42));
        var service = new ReportQueryService([handler], NullLogger<ReportQueryService>.Instance);

        var request = new ReportQueryRequest(
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["userId"] = ["123"],
            },
            Page: 2,
            PageSize: 10,
            SortBy: "userDisplayName",
            SortDirection: SortDirection.Desc,
            TimeZone: "America/Vancouver"
        );

        // Act
        var result = await service.ExecuteAsync("user-training", request, TestContext.Current.CancellationToken);

        // Assert
        var typedResult = Assert.IsType<FakePagedResponse>(result);
        Assert.Equal(2, typedResult.Page);
        Assert.Equal(10, typedResult.PageSize);
        Assert.Equal(42, typedResult.TotalRows);

        Assert.Equal(2, handler.LastPage);
        Assert.Equal(10, handler.LastPageSize);
        Assert.Equal("userDisplayName", handler.LastSortBy);
        Assert.Equal("Desc", handler.LastSortDirection);
        Assert.Equal("America/Vancouver", handler.LastTimeZone);
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
            new FakeReportQueryHandler("dup", new FakePagedResponse(1, 10, 0)),
            new FakeReportQueryHandler(" DUP ", new FakePagedResponse(1, 10, 0)),
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

        public int LastPage { get; private set; }

        public int LastPageSize { get; private set; }

        public string? LastSortBy { get; private set; }

        public string? LastSortDirection { get; private set; }

        public string? LastTimeZone { get; private set; }

        public Task<PagedResponse> ExecuteAsync(
            IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters,
            int page,
            int pageSize,
            string? sortBy,
            string? sortDirection,
            string? timeZone,
            CancellationToken cancellationToken = default
        )
        {
            LastFilters = filters;
            LastPage = page;
            LastPageSize = pageSize;
            LastSortBy = sortBy;
            LastSortDirection = sortDirection;
            LastTimeZone = timeZone;

            return Task.FromResult(response);
        }
    }

    private sealed record FakePagedResponse(int Page, int PageSize, int TotalRows)
        : PagedResponse(Page, PageSize, TotalRows);
}
