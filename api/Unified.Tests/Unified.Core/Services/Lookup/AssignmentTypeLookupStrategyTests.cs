using Microsoft.EntityFrameworkCore;
using Unified.Core.Models;
using Unified.Core.Services.Lookup;
using Unified.Db;
using Unified.Db.Models.Lookup;

namespace Unified.Tests.Core.Services.Lookup;

public class AssignmentTypeLookupStrategyTests : IAsyncLifetime
{
    private UnifiedDbContext _dbContext = null!;
    private AssignmentTypeLookupStrategy _strategy = null!;

    public ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UnifiedDbContext(options);
        _strategy = new AssignmentTypeLookupStrategy(_dbContext);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_AssignmentTypes()
    {
        _dbContext.AssignmentTypes.AddRange(
            new AssignmentType
            {
                Code = "court-support",
                Description = "Court Support",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
            new AssignmentType
            {
                Code = "transport",
                Description = "Transport",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _strategy.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Collection(
            result,
            first => Assert.Equal("court-support", first.Code),
            second => Assert.Equal("transport", second.Code)
        );
    }

    [Fact]
    public void CodeType_Should_Match_LookupCodeType_AssignmentTypes()
    {
        Assert.Equal(LookupCodeTypes.AssignmentTypes, _strategy.CodeType);
    }
}