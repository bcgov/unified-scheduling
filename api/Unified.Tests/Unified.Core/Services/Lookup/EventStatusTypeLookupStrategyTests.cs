using Microsoft.EntityFrameworkCore;
using Unified.Core.Models;
using Unified.Core.Services.Lookup;
using Unified.Db;
using Unified.Db.Models.Lookup;

namespace Unified.Tests.Core.Services.Lookup;

public class EventStatusTypeLookupStrategyTests : IAsyncLifetime
{
    private UnifiedDbContext _dbContext = null!;
    private EventStatusTypeLookupStrategy _strategy = null!;

    public ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UnifiedDbContext(options);
        _strategy = new EventStatusTypeLookupStrategy(_dbContext);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_EventStatusTypes()
    {
        _dbContext.EventStatusTypes.AddRange(
            new EventStatusType
            {
                Code = "active",
                Description = "Active",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
            new EventStatusType
            {
                Code = "draft",
                Description = "Draft",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _strategy.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Collection(
            result,
            first => Assert.Equal("active", first.Code),
            second => Assert.Equal("draft", second.Code)
        );
    }

    [Fact]
    public void CodeType_Should_Match_LookupCodeType_EventStatusTypes()
    {
        Assert.Equal(LookupCodeTypes.EventStatusTypes, _strategy.CodeType);
    }
}
