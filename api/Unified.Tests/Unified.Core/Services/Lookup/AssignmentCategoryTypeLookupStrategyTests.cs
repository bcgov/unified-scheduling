using Microsoft.EntityFrameworkCore;
using Unified.Core.Models;
using Unified.Core.Services.Lookup;
using Unified.Db;
using Unified.Db.Models.Lookup;

namespace Unified.Tests.Core.Services.Lookup;

public sealed class AssignmentCategoryTypeLookupStrategyTests : IAsyncLifetime
{
    private UnifiedDbContext _dbContext = null!;
    private AssignmentCategoryTypeLookupStrategy _strategy = null!;

    public ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UnifiedDbContext(options);
        _strategy = new AssignmentCategoryTypeLookupStrategy(_dbContext);

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_AssignmentCategories_With_ChildIds()
    {
        _dbContext.AssignmentCategoryTypes.Add(
            new AssignmentCategoryType
            {
                Id = 10,
                Code = "CourtRoom",
                Description = "Court Room",
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ChildCodeTypes =
                {
                    new AssignmentSubCategoryType
                    {
                        Id = 21,
                        Code = "SUPREME",
                        Description = "Supreme",
                        EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    },
                    new AssignmentSubCategoryType
                    {
                        Id = 20,
                        Code = "PROVINCIAL",
                        Description = "Provincial",
                        EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    },
                },
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _strategy.GetAllAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(result);
        Assert.Equal("CourtRoom", item.Code);
        Assert.Null(item.ParentCodeTypeId);
        Assert.Equal([20, 21], item.ChildCodeTypeIds);
    }

    [Fact]
    public void CodeType_Should_Match_LookupCodeType_AssignmentCategoryTypes()
    {
        Assert.Equal(LookupCodeTypes.AssignmentCategoryTypes, _strategy.CodeType);
    }
}
