using Microsoft.EntityFrameworkCore;
using Unified.Audit.Models;
using Unified.Audit.Services;
using Unified.Db;
using Unified.Db.Models;

namespace Unified.Tests.Unified.Audit.Services;

public class AuditHistoryServiceTests : IAsyncLifetime
{
    private UnifiedDbContext _dbContext = null!;
    private AuditHistoryService _service = null!;
    private readonly Guid _actorId = Guid.NewGuid();

    public ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UnifiedDbContext(options);
        _service = new AuditHistoryService(_dbContext, TimeProvider.System);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    private AuditRecord BuildRecord(
        string entityType = "User",
        string action = "Modified",
        string? actorName = "Jane Doe",
        Guid? actorUserId = null,
        string entityPK = "1",
        string[]? changedColumns = null,
        DateTimeOffset? occurredOn = null
    ) =>
        new()
        {
            OccurredOn = occurredOn ?? DateTimeOffset.UtcNow,
            ActorUserId = actorUserId ?? _actorId,
            ActorName = actorName,
            Action = action,
            EntityType = entityType,
            TableName = entityType + "s",
            EntityPK = entityPK,
            ChangedColumns = changedColumns ?? ["FirstName"],
        };

    [Fact]
    public async Task GetHistoryAsync_When_No_Filters_Should_Return_All_Records_In_Default_Week()
    {
        _dbContext.AuditRecords.AddRange(BuildRecord(), BuildRecord(entityType: "Role"));
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetHistoryAsync(
            new AuditHistoryQueryParams(),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Data.Count);
    }

    [Fact]
    public async Task GetHistoryAsync_When_Filtered_By_EntityType_Should_Return_Matching_Only()
    {
        _dbContext.AuditRecords.AddRange(BuildRecord(entityType: "User"), BuildRecord(entityType: "Role"));
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetHistoryAsync(
            new AuditHistoryQueryParams { EntityType = "Role" },
            TestContext.Current.CancellationToken
        );

        Assert.Single(result.Data);
        Assert.Equal("Role", result.Data[0].EntityType);
    }

    [Fact]
    public async Task GetHistoryAsync_When_Filtered_By_Action_Should_Return_Matching_Only()
    {
        _dbContext.AuditRecords.AddRange(BuildRecord(action: "Added"), BuildRecord(action: "Deleted"));
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetHistoryAsync(
            new AuditHistoryQueryParams { Action = "Added" },
            TestContext.Current.CancellationToken
        );

        Assert.Single(result.Data);
        Assert.Equal("Added", result.Data[0].Action);
    }

    [Fact]
    public async Task GetHistoryAsync_When_Filtered_By_ChangedField_Should_Return_Matching_Only()
    {
        _dbContext.AuditRecords.AddRange(
            BuildRecord(changedColumns: ["FirstName"]),
            BuildRecord(changedColumns: ["LastName"])
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetHistoryAsync(
            new AuditHistoryQueryParams { ChangedField = ["LastName"] },
            TestContext.Current.CancellationToken
        );

        Assert.Single(result.Data);
        Assert.Contains("LastName", result.Data[0].ChangedColumns!);
    }

    [Fact]
    public async Task GetHistoryAsync_When_Filtered_By_Multiple_ChangedFields_Should_Require_All_To_Match()
    {
        _dbContext.AuditRecords.AddRange(
            BuildRecord(changedColumns: ["FirstName", "LastName"]),
            BuildRecord(changedColumns: ["FirstName"]),
            BuildRecord(changedColumns: ["LastName"])
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetHistoryAsync(
            new AuditHistoryQueryParams { ChangedField = ["FirstName", "LastName"] },
            TestContext.Current.CancellationToken
        );

        Assert.Single(result.Data);
        Assert.Contains("FirstName", result.Data[0].ChangedColumns!);
        Assert.Contains("LastName", result.Data[0].ChangedColumns!);
    }

    [Fact]
    public async Task GetHistoryAsync_When_Filtered_By_ActorUserId_Should_Return_Matching_Only()
    {
        var otherActor = Guid.NewGuid();
        _dbContext.AuditRecords.AddRange(BuildRecord(actorUserId: _actorId), BuildRecord(actorUserId: otherActor));
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetHistoryAsync(
            new AuditHistoryQueryParams { ActorUserId = otherActor },
            TestContext.Current.CancellationToken
        );

        Assert.Single(result.Data);
        Assert.Equal(otherActor, result.Data[0].ActorUserId);
    }

    [Fact]
    public async Task GetHistoryAsync_When_Filtered_By_ActorName_Should_Match_Case_Insensitive_Partial()
    {
        _dbContext.AuditRecords.AddRange(BuildRecord(actorName: "Jane Doe"), BuildRecord(actorName: "John Smith"));
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetHistoryAsync(
            new AuditHistoryQueryParams { ActorName = "jane" },
            TestContext.Current.CancellationToken
        );

        Assert.Single(result.Data);
        Assert.Equal("Jane Doe", result.Data[0].ActorName);
    }

    [Fact]
    public async Task GetHistoryAsync_When_Sorted_Ascending_Should_Return_Oldest_First()
    {
        var older = BuildRecord(occurredOn: DateTimeOffset.UtcNow.AddHours(-1));
        var newer = BuildRecord(occurredOn: DateTimeOffset.UtcNow);
        _dbContext.AuditRecords.AddRange(older, newer);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetHistoryAsync(
            new AuditHistoryQueryParams { SortDirection = "asc" },
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Data[0].OccurredOn <= result.Data[1].OccurredOn);
    }

    [Fact]
    public async Task GetHistoryAsync_When_Page_Beyond_Last_Page_Should_Return_Empty_Data_With_Correct_TotalCount()
    {
        _dbContext.AuditRecords.AddRange(BuildRecord(), BuildRecord());
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetHistoryAsync(
            new AuditHistoryQueryParams { Page = 5, PageSize = 10 },
            TestContext.Current.CancellationToken
        );

        Assert.Empty(result.Data);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetHistoryAsync_When_No_Records_Match_Should_Return_Empty_Result()
    {
        var result = await _service.GetHistoryAsync(
            new AuditHistoryQueryParams(),
            TestContext.Current.CancellationToken
        );

        Assert.Empty(result.Data);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetHistoryAsync_When_Outside_Date_Range_Should_Be_Excluded()
    {
        _dbContext.AuditRecords.Add(BuildRecord(occurredOn: DateTimeOffset.UtcNow.AddDays(-30)));
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetHistoryAsync(
            new AuditHistoryQueryParams(),
            TestContext.Current.CancellationToken
        );

        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetRecordedEntityTypesAsync_Should_Return_Distinct_Sorted_Types()
    {
        _dbContext.AuditRecords.AddRange(
            BuildRecord(entityType: "User"),
            BuildRecord(entityType: "Role"),
            BuildRecord(entityType: "User")
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetRecordedEntityTypesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Role", "User"], result);
    }
}
