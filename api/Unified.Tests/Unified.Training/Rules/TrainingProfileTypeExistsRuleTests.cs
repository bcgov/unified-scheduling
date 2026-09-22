using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Training.Rules;
using Xunit;
using TrainingProfileLinkEntity = Unified.Db.Models.Training.TrainingProfile;
using TrainingProfileTypeEntity = Unified.Db.Models.Training.TrainingProfileType;

namespace Unified.Tests.Training.Rules;

public sealed class TrainingProfileTypeExistsRuleTests : IAsyncLifetime
{
    private UnifiedDbContext _dbContext = null!;
    private readonly TrainingProfileTypeExistsRule _rule = new();

    public ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UnifiedDbContext(options);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoPendingTrainingProfileLinks_ShouldPass()
    {
        await _rule.ExecuteAsync(_dbContext, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllReferencedProfileTypesExist_ShouldPass()
    {
        var profileType = new TrainingProfileTypeEntity
        {
            Id = 200,
            Code = "CARBINE",
            Name = "Carbine",
        };

        _dbContext.TrainingProfileTypes.Add(profileType);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _dbContext.TrainingProfiles.Add(
            new TrainingProfileLinkEntity { TrainingId = 1, TrainingProfileTypeId = profileType.Id }
        );

        await _rule.ExecuteAsync(_dbContext, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReferencedProfileTypeMissing_ShouldThrow()
    {
        _dbContext.TrainingProfiles.Add(new TrainingProfileLinkEntity { TrainingId = 1, TrainingProfileTypeId = 999 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _rule.ExecuteAsync(_dbContext, TestContext.Current.CancellationToken)
        );

        Assert.Contains("Training profile type id(s) not found", ex.Message);
        Assert.Contains("999", ex.Message);
    }
}
