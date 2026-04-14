using Microsoft.EntityFrameworkCore;
using Unified.Core.Models;
using Unified.Core.Services.Lookup;
using Unified.Db;
using Unified.Db.Models.Lookup;

namespace Unified.Tests.Core.Services.Lookup;

public class PositionTypeLookupStrategyTests : IAsyncLifetime
{
    private UnifiedDbContext _dbContext = null!;
    private PositionTypeLookupStrategy _strategy = null!;

    public ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UnifiedDbContext(options);
        _strategy = new PositionTypeLookupStrategy(_dbContext);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_PositionTypes()
    {
        // Arrange
        _dbContext.PositionTypes.AddRange(
            new PositionType
            {
                Code = "SERGEANT",
                Description = "Sergeant",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
            new PositionType
            {
                Code = "DEPUTY",
                Description = "Deputy Sheriff",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _strategy.GetAllAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.Code == "SERGEANT");
        Assert.Contains(result, x => x.Code == "DEPUTY");
    }

    [Fact]
    public void CodeType_Should_Match_LookupCodeType_PositionTypes()
    {
        Assert.Equal(LookupCodeTypes.PositionTypes, _strategy.CodeType);
    }
}
