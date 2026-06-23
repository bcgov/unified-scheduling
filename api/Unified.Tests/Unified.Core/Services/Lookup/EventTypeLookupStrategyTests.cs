using Microsoft.EntityFrameworkCore;
using Unified.Core.Models;
using Unified.Core.Services.Lookup;
using Unified.Db;
using Unified.Db.Models.Lookup;

namespace Unified.Tests.Core.Services.Lookup;

public class EventTypeLookupStrategyTests : IAsyncLifetime
{
    private UnifiedDbContext _dbContext = null!;
    private EventTypeLookupStrategy _strategy = null!;

    public ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UnifiedDbContext(options);
        _strategy = new EventTypeLookupStrategy(_dbContext);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_EventTypes()
    {
        _dbContext.EventTypes.AddRange(
            new EventType
            {
                Code = "deadline",
                Description = "Deadline",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
            new EventType
            {
                Code = "holiday",
                Description = "Holiday",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _strategy.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Collection(
            result,
            first => Assert.Equal("deadline", first.Code),
            second => Assert.Equal("holiday", second.Code)
        );
    }

    [Fact]
    public void CodeType_Should_Match_LookupCodeType_EventTypes()
    {
        Assert.Equal(LookupCodeTypes.EventTypes, _strategy.CodeType);
    }
}
