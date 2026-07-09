using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Db;
using Unified.Db.Models;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;

namespace Unified.Tests.Scheduling.Services;

public sealed class AssignmentTypeServiceTests : IAsyncLifetime
{
    private UnifiedDbContext _dbContext = null!;
    private AssignmentTypeService _service = null!;

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
                Id = 9,
                AgencyId = "A9",
                Name = "Location 9",
                Timezone = "America/Vancouver",
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _service = new AssignmentTypeService(NullLogger<AssignmentTypeService>.Instance, _dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task CreateAssignmentTypeAsync_WhenCodeExistsForDifferentLocation_CreatesAssignmentType()
    {
        await _service.CreateAssignmentTypeAsync(
            CreateRequest(locationId: 5, code: "control"),
            TestContext.Current.CancellationToken
        );

        var result = await _service.CreateAssignmentTypeAsync(
            CreateRequest(locationId: 9, code: " control "),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(9, result.LocationId);
        Assert.Equal("CONTROL", result.Code);
        Assert.Equal(2, await _dbContext.AssignmentTypes.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAssignmentTypeAsync_WhenCodeExistsForSameLocation_ThrowsInvalidOperationException()
    {
        await _service.CreateAssignmentTypeAsync(
            CreateRequest(locationId: 5, code: "control"),
            TestContext.Current.CancellationToken
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAssignmentTypeAsync(
                CreateRequest(locationId: 5, code: "CONTROL"),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("already exists for location 5", exception.Message);
    }

    [Fact]
    public async Task GetAssignmentTypesAsync_WhenLocationProvided_ReturnsOnlyThatLocation()
    {
        await _service.CreateAssignmentTypeAsync(
            CreateRequest(locationId: 5, code: "control"),
            TestContext.Current.CancellationToken
        );
        await _service.CreateAssignmentTypeAsync(
            CreateRequest(locationId: 9, code: "security"),
            TestContext.Current.CancellationToken
        );

        var result = await _service.GetAssignmentTypesAsync(5, TestContext.Current.CancellationToken);

        var assignmentType = Assert.Single(result);
        Assert.Equal(5, assignmentType.LocationId);
        Assert.Equal("CONTROL", assignmentType.Code);
    }

    private static AssignmentTypeRequest CreateRequest(int locationId, string code) =>
        new()
        {
            LocationId = locationId,
            Code = code,
            Description = "Control",
            EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
}
