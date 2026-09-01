using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Lookup;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;

namespace Unified.Tests.Scheduling.Services;

public sealed class AssignmentDefinitionServiceTests : IAsyncLifetime
{
    private UnifiedDbContext _dbContext = null!;
    private AssignmentDefinitionService _service = null!;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UnifiedDbContext(options);
        _dbContext.Locations.AddRange(
            new Location
            {
                Id = 5,
                AgencyId = "A5",
                Name = "Location 5",
                Timezone = "America/Vancouver",
            },
            new Location
            {
                Id = 6,
                AgencyId = "A6",
                Name = "Location 6",
                Timezone = "America/Vancouver",
            }
        );
        _dbContext.AssignmentCategoryTypes.Add(
            new AssignmentCategoryType
            {
                Id = 10,
                Code = "CAT",
                Description = "Category",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );
        _dbContext.AssignmentSubCategoryTypes.Add(
            new AssignmentSubCategoryType
            {
                Id = 20,
                ParentCodeTypeId = 10,
                Code = "SUB",
                Description = "Subcategory",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _service = new AssignmentDefinitionService(NullLogger<AssignmentDefinitionService>.Instance, _dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetAssignmentDefinitionsAsync_ReturnsDefinitionsWithDefaultsAndLookupDetails()
    {
        await _service.CreateAssignmentDefinitionAsync(
            CreateRequest(name: "control"),
            TestContext.Current.CancellationToken
        );
        await _service.CreateAssignmentDefinitionAsync(
            CreateRequest(
                name: "expired",
                effectiveDate: new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                expiryDate: new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );

        var result = await _service.GetAssignmentDefinitionsAsync(
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(["control", "expired"], result.Select(definition => definition.Name));

        var definition = Assert.Single(result, definition => definition.Name == "control");
        Assert.Equal(5, definition.LocationId);
        Assert.Equal("Control", definition.Description);
        Assert.Equal(10, definition.AssignmentCategoryTypeId);
        Assert.Equal("Category", definition.AssignmentCategoryTypeDescription);
        Assert.Equal(20, definition.AssignmentSubCategoryTypeId);
        Assert.Equal("Subcategory", definition.AssignmentSubCategoryTypeDescription);
        Assert.Equal("blue", definition.Color);
        Assert.Equal("08:00:00", definition.DefaultStartTime);
        Assert.Equal("15:00:00", definition.DefaultEndTime);
        Assert.Equal(3, definition.DefaultCapacity);
        Assert.Equal(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), definition.EffectiveDateUtc);
        Assert.Null(definition.ExpiryDateUtc);
    }

    [Fact]
    public async Task GetAssignmentDefinitionsAsync_ReturnsFutureAndExpiredDefinitionsForFrontendDateFiltering()
    {
        await _service.CreateAssignmentDefinitionAsync(
            CreateRequest(name: "current"),
            TestContext.Current.CancellationToken
        );
        await _service.CreateAssignmentDefinitionAsync(
            CreateRequest(name: "future", effectiveDate: DateTimeOffset.UtcNow.AddDays(7)),
            TestContext.Current.CancellationToken
        );
        await _service.CreateAssignmentDefinitionAsync(
            CreateRequest(
                name: "expired",
                effectiveDate: new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                expiryDate: new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero)
            ),
            TestContext.Current.CancellationToken
        );

        var result = await _service.GetAssignmentDefinitionsAsync(5, TestContext.Current.CancellationToken);

        Assert.Equal(["current", "expired", "future"], result.Select(definition => definition.Name));
    }

    [Fact]
    public async Task GetAssignmentDefinitionsAsync_WhenLocationIdProvided_ReturnsDefinitionsForLocation()
    {
        await _service.CreateAssignmentDefinitionAsync(
            CreateRequest(name: "control"),
            TestContext.Current.CancellationToken
        );
        await _service.CreateAssignmentDefinitionAsync(
            CreateRequest(name: "transport", locationId: 6),
            TestContext.Current.CancellationToken
        );

        var result = await _service.GetAssignmentDefinitionsAsync(5, TestContext.Current.CancellationToken);

        var definition = Assert.Single(result);
        Assert.Equal("control", definition.Name);
        Assert.Equal(5, definition.LocationId);
    }

    [Fact]
    public async Task CreateAssignmentDefinitionAsync_WhenNameExists_ThrowsInvalidOperationException()
    {
        await _service.CreateAssignmentDefinitionAsync(
            CreateRequest(name: "control"),
            TestContext.Current.CancellationToken
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAssignmentDefinitionAsync(
                CreateRequest(name: " CONTROL "),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("already exists", exception.Message);
    }

    [Fact]
    public async Task CreateAssignmentDefinitionAsync_PreservesNameCasing()
    {
        var result = await _service.CreateAssignmentDefinitionAsync(
            CreateRequest(name: "Office Standards"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal("Office Standards", result.Name);
    }

    [Fact]
    public async Task CreateAssignmentDefinitionAsync_WhenDescriptionIsNull_SavesNullDescription()
    {
        var result = await _service.CreateAssignmentDefinitionAsync(
            CreateRequest(name: "No description", description: null),
            TestContext.Current.CancellationToken
        );

        Assert.Null(result.Description);
    }

    private static AssignmentDefinitionRequest CreateRequest(
        string name,
        int locationId = 5,
        DateTimeOffset? effectiveDate = null,
        DateTimeOffset? expiryDate = null,
        string? color = " blue ",
        string? description = "Control"
    ) =>
        new()
        {
            LocationId = locationId,
            Name = name,
            Description = description,
            AssignmentCategoryTypeId = 10,
            AssignmentSubCategoryTypeId = 20,
            Color = color,
            DefaultStartTime = "08:00:00",
            DefaultEndTime = "15:00:00",
            DefaultCapacity = 3,
            EffectiveDateUtc = effectiveDate ?? new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ExpiryDateUtc = expiryDate,
        };
}
