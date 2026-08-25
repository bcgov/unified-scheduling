using Microsoft.Extensions.Logging.Abstractions;
using Unified.Common.Reporting;
using Unified.Reporting.Models.Reporting;
using Unified.Reporting.Services.Reporting;

namespace Unified.Tests.Reporting.Services;

public class ReportQueryServiceTests
{
    [Fact]
    public async Task ExecuteAsync_Should_Map_Handler_Result_And_Forward_Request_Values()
    {
        // Arrange
        var handler = new FakeReportQueryHandler(
            "user-training",
            [
                BuildColumn("userDisplayName", "User", "String", true),
                BuildColumn("trainingId", "ID", "Number", false),
            ],
            [(IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["userDisplayName"] = "Doe, Jane" }],
            42
        );

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
        Assert.Equal("user-training", result.ReportKey);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(42, result.TotalRows);

        var firstColumn = Assert.Single(result.Columns, column => column.Key == "userDisplayName");
        Assert.Equal(ReportValueType.String, firstColumn.Type);
        Assert.True(firstColumn.Sortable);

        var secondColumn = Assert.Single(result.Columns, column => column.Key == "trainingId");
        Assert.Equal(ReportValueType.Number, secondColumn.Type);
        Assert.False(secondColumn.Sortable);

        Assert.Equal("user-training", handler.LastReportKey);
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
            new FakeReportQueryHandler("dup", [], [], 0),
            new FakeReportQueryHandler(" DUP ", [], [], 0),
        };

        // Act + Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ReportQueryService(handlers, NullLogger<ReportQueryService>.Instance)
        );

        Assert.Contains("Duplicate report handler registration", ex.Message);
    }

    private static IReadOnlyDictionary<string, object?> BuildColumn(string key, string label, string type, bool sortable)
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["key"] = key,
            ["label"] = label,
            ["type"] = type,
            ["sortable"] = sortable,
        };
    }

    private sealed class FakeReportQueryHandler(
        string reportKey,
        IReadOnlyCollection<IReadOnlyDictionary<string, object?>> columns,
        IReadOnlyCollection<IReadOnlyDictionary<string, object?>> rows,
        int totalRows
    ) : IReportQueryHandler
    {
        public string ReportKey => reportKey;

        public string? LastReportKey { get; private set; }

        public IReadOnlyDictionary<string, IReadOnlyCollection<string>> LastFilters { get; private set; } =
            new Dictionary<string, IReadOnlyCollection<string>>();

        public int LastPage { get; private set; }

        public int LastPageSize { get; private set; }

        public string? LastSortBy { get; private set; }

        public string? LastSortDirection { get; private set; }

        public string? LastTimeZone { get; private set; }

        public Task<(
            IReadOnlyCollection<IReadOnlyDictionary<string, object?>> Columns,
            IReadOnlyCollection<IReadOnlyDictionary<string, object?>> Rows,
            int TotalRows
        )> ExecuteAsync(
            IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters,
            int page,
            int pageSize,
            string? sortBy,
            string? sortDirection,
            string? timeZone,
            CancellationToken cancellationToken = default
        )
        {
            LastReportKey = reportKey;
            LastFilters = filters;
            LastPage = page;
            LastPageSize = pageSize;
            LastSortBy = sortBy;
            LastSortDirection = sortDirection;
            LastTimeZone = timeZone;

            return Task.FromResult((columns, rows, totalRows));
        }
    }
}