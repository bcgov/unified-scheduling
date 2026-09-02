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
                                ["userId"] = "abc-123",
                            }
                        ),
                    },
                },
            },
        };

        // Act
        var result = await controller.Get(
            "user-training",
            new ReportQueryParameters(Page: 2, PageSize: 25, SortBy: "userDisplayName", SortDir: "desc"),
            TestContext.Current.CancellationToken
        );

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        _ = Assert.IsType<FakePagedResponse>(okResult.Value);

        Assert.Equal("user-training", service.LastReportKey);
        Assert.NotNull(service.LastRequest);
        Assert.Equal("userDisplayName", service.LastRequest.SortBy);
        Assert.Equal(SortDirection.Desc, service.LastRequest.SortDirection);
        Assert.True(service.LastRequest.Filters.ContainsKey("userId"));
        Assert.False(service.LastRequest.Filters.ContainsKey("page"));
        Assert.False(service.LastRequest.Filters.ContainsKey("pageSize"));
        Assert.False(service.LastRequest.Filters.ContainsKey("sortBy"));
        Assert.False(service.LastRequest.Filters.ContainsKey("sortDir"));
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
        var result = await controller.Get(
            "user-training",
            new ReportQueryParameters(SortDir: "sideways"),
            TestContext.Current.CancellationToken
        );

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("Invalid report query request", problemDetails.Title);
    }

    [Fact]
    public async Task Get_Should_Use_Typed_Custom_Filters_And_Ignore_Filter_Transport_Keys()
    {
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
                                ["trainingId"] = "9",
                                ["filters[userId]"] = "transport-only",
                            }
                        ),
                    },
                },
            },
        };

        var result = await controller.Get(
            "user-training",
            new ReportQueryParameters(
                Filters: new Dictionary<string, string?>
                {
                    ["userId"] = "typed-user",
                    ["trainingId"] = "12",
                    ["status"] = "active",
                }
            ),
            TestContext.Current.CancellationToken
        );

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        _ = Assert.IsType<FakePagedResponse>(okResult.Value);

        Assert.NotNull(service.LastRequest);
        Assert.Equal("typed-user", Assert.Single(service.LastRequest.Filters["userId"]));
        Assert.Equal("12", Assert.Single(service.LastRequest.Filters["trainingId"]));
        Assert.Equal("active", Assert.Single(service.LastRequest.Filters["status"]));
        Assert.False(service.LastRequest.Filters.ContainsKey("filters[userId]"));
    }

    private sealed class FakeReportQueryService : IReportQueryService
    {
        public string? LastReportKey { get; private set; }

        public ReportQueryRequest? LastRequest { get; private set; }

        public Task<PagedResponse> ExecuteAsync(
            string reportKey,
            ReportQueryRequest request,
            CancellationToken cancellationToken = default
        )
        {
            LastReportKey = reportKey;
            LastRequest = request;

            return Task.FromResult<PagedResponse>(new FakePagedResponse(0));
        }
    }

    private sealed record FakePagedResponse(int TotalRows) : PagedResponse(TotalRows);
}
