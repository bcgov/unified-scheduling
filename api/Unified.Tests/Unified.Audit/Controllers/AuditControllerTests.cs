using Microsoft.AspNetCore.Mvc;
using Unified.Audit.Controllers;
using Unified.Audit.Models;
using Unified.Audit.Services;
using Unified.Audit.Validators;

namespace Unified.Tests.Unified.Audit.Controllers;

public class AuditControllerTests
{
    private static AuditController BuildController(
        FakeAuditHistoryService? historyService = null,
        FakeAuditSchemaService? schemaService = null
    ) =>
        new(
            historyService ?? new FakeAuditHistoryService(),
            schemaService ?? new FakeAuditSchemaService(),
            new AuditHistoryQueryParamsValidator()
        );

    [Fact]
    public async Task GetHistory_When_Valid_Should_Return_Ok()
    {
        var expected = new AuditHistoryResponse
        {
            Page = 1,
            PageSize = 25,
            TotalCount = 0,
            Data = [],
        };
        var controller = BuildController(historyService: new FakeAuditHistoryService { HistoryResult = expected });

        var result = await controller.GetHistory(new AuditHistoryQueryParams(), TestContext.Current.CancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, okResult.Value);
    }

    [Fact]
    public async Task GetHistory_When_EntityType_Unknown_Should_Return_NotFound()
    {
        var controller = BuildController(schemaService: new FakeAuditSchemaService { KnownEntityTypes = [] });

        var result = await controller.GetHistory(
            new AuditHistoryQueryParams { EntityType = "NotAnEntity" },
            TestContext.Current.CancellationToken
        );

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetHistory_When_Invalid_QueryParams_Should_Throw_ValidationException()
    {
        var controller = BuildController();

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(
            () =>
                controller.GetHistory(
                    new AuditHistoryQueryParams { PageSize = 101 },
                    TestContext.Current.CancellationToken
                )
        );
    }

    [Fact]
    public async Task GetEntityTypes_Should_Return_Ok_With_Types()
    {
        var controller = BuildController(
            historyService: new FakeAuditHistoryService { EntityTypesResult = ["Role", "User"] }
        );

        var result = await controller.GetEntityTypes(TestContext.Current.CancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AuditEntityTypesResponse>(okResult.Value);
        Assert.Equal(["Role", "User"], response.EntityTypes);
    }

    [Fact]
    public void GetEntityTypeFields_When_Known_Type_Should_Return_Ok()
    {
        var expected = new AuditEntityFieldsResponse
        {
            EntityType = "User",
            Fields = [new AuditEntityFieldDto { Name = "FirstName", Label = "First Name", Type = "string" }],
        };
        var controller = BuildController(
            schemaService: new FakeAuditSchemaService { FieldsResult = expected, KnownEntityTypes = ["User"] }
        );

        var result = controller.GetEntityTypeFields("User");

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, okResult.Value);
    }

    [Fact]
    public void GetEntityTypeFields_When_Unknown_Type_Should_Return_NotFound()
    {
        var controller = BuildController(schemaService: new FakeAuditSchemaService { FieldsResult = null });

        var result = controller.GetEntityTypeFields("NotAnEntity");

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private sealed class FakeAuditHistoryService : IAuditHistoryService
    {
        public AuditHistoryResponse HistoryResult { get; set; } =
            new()
            {
                Page = 1,
                PageSize = 25,
                TotalCount = 0,
                Data = [],
            };
        public IReadOnlyList<string> EntityTypesResult { get; set; } = [];

        public Task<AuditHistoryResponse> GetHistoryAsync(
            AuditHistoryQueryParams queryParams,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(HistoryResult);

        public Task<IReadOnlyList<string>> GetRecordedEntityTypesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(EntityTypesResult);
    }

    private sealed class FakeAuditSchemaService : IAuditSchemaService
    {
        public IReadOnlyList<string> KnownEntityTypes { get; set; } = ["User"];
        public AuditEntityFieldsResponse? FieldsResult { get; set; }

        public bool EntityTypeExists(string entityType) =>
            KnownEntityTypes.Contains(entityType, StringComparer.OrdinalIgnoreCase);

        public AuditEntityFieldsResponse? GetFields(string entityType) => FieldsResult;
    }
}
