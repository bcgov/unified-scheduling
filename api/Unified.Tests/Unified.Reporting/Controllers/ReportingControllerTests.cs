using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Unified.Common.Reporting;
using Unified.Reporting.Controllers;
using Unified.Reporting.Models;
using Unified.Reporting.Services.Reporting;

namespace Unified.Tests.Reporting.Controllers;

public class ReportingControllerTests
{
    [Fact]
    public async Task Get_Should_Parse_Query_And_Exclude_Reserved_Keys()
    {
        // Arrange
        var service = new FakeReportQueryService();
        var controller = new ReportingController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request =
                    {
                        Query = new QueryCollection(
                            new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["page"] = "2",
                                ["pageSize"] = "25",
                                ["sortBy"] = "userDisplayName",
                                ["sortDir"] = "desc",
                                ["tz"] = "America/Vancouver",
                                ["userId"] = "abc-123",
                            }
                        ),
                    },
                },
            },
        };

        // Act
        var result = await controller.Get("user-training", TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        _ = Assert.IsType<FakePagedResponse>(okResult.Value);

        Assert.Equal("user-training", service.LastReportKey);
        Assert.NotNull(service.LastRequest);
        Assert.Equal(2, service.LastRequest!.Page);
        Assert.Equal(25, service.LastRequest.PageSize);
        Assert.Equal("userDisplayName", service.LastRequest.SortBy);
        Assert.Equal(SortDirection.Desc, service.LastRequest.SortDirection);
        Assert.Equal("America/Vancouver", service.LastRequest.TimeZone);
        Assert.True(service.LastRequest.Filters.ContainsKey("userId"));
        Assert.False(service.LastRequest.Filters.ContainsKey("page"));
        Assert.False(service.LastRequest.Filters.ContainsKey("pageSize"));
        Assert.False(service.LastRequest.Filters.ContainsKey("sortBy"));
        Assert.False(service.LastRequest.Filters.ContainsKey("sortDir"));
        Assert.False(service.LastRequest.Filters.ContainsKey("tz"));
    }

    [Fact]
    public async Task Get_Should_Return_BadRequest_When_SortDir_Is_Invalid()
    {
        // Arrange
        var service = new FakeReportQueryService();
        var controller = new ReportingController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request =
                    {
                        Query = new QueryCollection(
                            new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["sortDir"] = "sideways",
                            }
                        ),
                    },
                },
            },
        };

        // Act
        var result = await controller.Get("user-training", TestContext.Current.CancellationToken);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("Invalid report query request", problemDetails.Title);
    }

    private sealed class FakeReportQueryService : IReportQueryService
    {
        public string? LastReportKey { get; private set; }

        public ReportQueryRequest? LastRequest { get; private set; }

        public Task<PaginatableResponse> ExecuteAsync(
            string reportKey,
            ReportQueryRequest request,
            CancellationToken cancellationToken = default
        )
        {
            LastReportKey = reportKey;
            LastRequest = request;

            return Task.FromResult<PaginatableResponse>(new FakePagedResponse(request.Page, request.PageSize, 0));
        }
    }

    private sealed record FakePagedResponse(int Page, int PageSize, int TotalRows)
        : PaginatableResponse(Page, PageSize, TotalRows);
}
