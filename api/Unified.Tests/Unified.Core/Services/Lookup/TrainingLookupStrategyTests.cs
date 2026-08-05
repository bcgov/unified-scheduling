using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models.Training;
using Unified.Tests.TestHelpers;
using Unified.Training.Models;
using Unified.Training.Services.Lookup;
using LookupCodeTypes = Unified.Core.Models.LookupCodeTypes;
using TrainingEntity = Unified.Db.Models.Training.Training;

namespace Unified.Tests.Core.Services.Lookup;

public class TrainingLookupStrategyTests : IAsyncLifetime
{
    private UnifiedDbContext _dbContext = null!;
    private TrainingLookupStrategy _strategy = null!;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseSqlite("Data Source=:memory:;Cache=Shared;Mode=Memory;")
            .Options;

        _dbContext = new SqliteTestUnifiedDbContext(options);
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();

        _dbContext.TrainingCategories.Add(new TrainingCategory { Id = 1, Name = "Mandatory" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _strategy = new TrainingLookupStrategy(_dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.Database.CloseConnectionAsync();
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_Trainings_In_Order()
    {
        _dbContext.Trainings.AddRange(
            new TrainingEntity
            {
                Code = "E",
                Description = "Entry",
                Order = 1,
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
            new TrainingEntity
            {
                Code = "A",
                Description = "Annual",
                Order = 0,
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _strategy.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Collection(result, first => Assert.Equal("A", first.Code), second => Assert.Equal("E", second.Code));
    }

    [Fact]
    public async Task CreateAsync_Should_Create_Training_Response()
    {
        var result = await _strategy.CreateAsync(
            new TrainingLookupRequest
            {
                Code = " FIRE ",
                Description = " Firearms Qualification ",
                TrainingCategoryId = 1,
                ValidityDays = 365,
                Mandatory = true,
            },
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Id > 0);
        Assert.Equal("FIRE", result.Code);
        Assert.Equal("Firearms Qualification", result.Description);
        Assert.Equal(1, result.TrainingCategoryId);
        Assert.Equal("Mandatory", result.TrainingCategoryName);
        Assert.True(result.Mandatory);
    }

    [Fact]
    public async Task UpdateAsync_Should_Return_Null_When_Not_Found()
    {
        var result = await _strategy.UpdateAsync(
            999,
            new TrainingLookupRequest { Code = "ABC", Description = "Test" },
            TestContext.Current.CancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task MoveOrderAsync_Should_Reorder_Trainings()
    {
        _dbContext.Trainings.AddRange(
            new TrainingEntity
            {
                Code = "A",
                Description = "Annual",
                Order = 0,
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
            new TrainingEntity
            {
                Code = "B",
                Description = "Basic",
                Order = 1,
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
            new TrainingEntity
            {
                Code = "C",
                Description = "Core",
                Order = 2,
                EffectiveDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var movedId = await _dbContext
            .Trainings.Where(t => t.Code == "C")
            .Select(t => t.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        var result = await _strategy.MoveOrderAsync(movedId, 0, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("C", result.Code);

        var orderedCodes = await _dbContext
            .Trainings.OrderBy(t => t.Order)
            .Select(t => t.Code)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["C", "A", "B"], orderedCodes);
    }

    [Fact]
    public void CodeType_Should_Match_LookupCodeType_Trainings()
    {
        Assert.Equal(LookupCodeTypes.Trainings, _strategy.CodeType);
    }
}
