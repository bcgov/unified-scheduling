using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Unified.Common.Interceptors;
using Unified.Db;
using Unified.Tests.TestHelpers;
using Unified.Training.Rules;
using Xunit;
using TrainingEntity = Unified.Db.Models.Training.Training;
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

    [Fact]
    public async Task SaveChangesAsync_WithRegisteredRule_ShouldRejectMissingProfileType_AndNotPersistLink()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ISaveRule, TrainingProfileTypeExistsRule>();
        services.AddScoped<SaveRulesInterceptor>();

        await using var serviceProvider = services.BuildServiceProvider();

        var options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(serviceProvider.GetRequiredService<SaveRulesInterceptor>())
            .Options;

        await using (var context = new SqliteTestUnifiedDbContext(options))
        {
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

            var training = new TrainingEntity
            {
                Code = "MANDATORY-1",
                Description = "Mandatory 1",
                EffectiveDate = DateTimeOffset.UtcNow,
            };

            context.Trainings.Add(training);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            context.TrainingProfiles.Add(
                new TrainingProfileLinkEntity { TrainingId = training.Id, TrainingProfileTypeId = 999 }
            );

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                context.SaveChangesAsync(TestContext.Current.CancellationToken)
            );

            Assert.Contains("Training profile type id(s) not found", ex.Message);
            Assert.Contains("999", ex.Message);
        }

        var verifyOptions = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(connection).Options;
        await using var verifyContext = new SqliteTestUnifiedDbContext(verifyOptions);

        var persistedLinks = await verifyContext
            .TrainingProfiles.AsNoTracking()
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, persistedLinks);
    }
}
