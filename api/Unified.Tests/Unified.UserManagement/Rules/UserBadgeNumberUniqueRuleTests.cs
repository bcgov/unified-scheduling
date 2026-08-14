using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Unified.Common.Interceptors;
using Unified.Db;
using Unified.Db.Models.UserManagement;
using Unified.UserManagement.FeatureFlags;
using Unified.UserManagement.Rules;
using Xunit;

namespace Unified.Tests.UserManagement.Rules;

public class UserBadgeNumberUniqueRuleTests : IAsyncLifetime
{
    private UnifiedDbContext _dbContext = null!;

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

    private UserBadgeNumberUniqueRule CreateRule(bool enabled = true)
    {
        var flags = new UserManagementFeatureFlags
        {
            Enabled = enabled,
            UserBadgeNumber = new UserBadgeNumberFlags { Enabled = enabled },
        };

        var optionsMonitor = new FakeOptionsMonitor<UserManagementFeatureFlags>(flags);
        return new UserBadgeNumberUniqueRule(optionsMonitor);
    }

    [Fact]
    public async Task ExecuteAsync_FeatureFlagDisabled_SkipsValidation()
    {
        // Arrange
        var rule = CreateRule(enabled: false);

        var user = new User
        {
            Id = Guid.NewGuid(),
            IdirName = "test",
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            Gender = Gender.Female,
            Rank = "Rank1",
            BadgeNumber = null,
        };
        _dbContext.Users.Add(user);

        // Act & Assert - should not throw even with null badge
        await rule.ExecuteAsync(_dbContext, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_NewUserWithUniqueBadgeNumber_Passes()
    {
        // Arrange
        var rule = CreateRule();

        var user = new User
        {
            Id = Guid.NewGuid(),
            IdirName = "testuser",
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            Gender = Gender.Female,
            Rank = "Rank1",
            BadgeNumber = "BADGE123",
        };
        _dbContext.Users.Add(user);

        // Act & Assert - should not throw
        await rule.ExecuteAsync(_dbContext, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_NewUserWithMissingBadgeNumber_ThrowsWhenRequired()
    {
        // Arrange
        var rule = CreateRule();

        var user = new User
        {
            Id = Guid.NewGuid(),
            IdirName = "testuser",
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            Gender = Gender.Male,
            Rank = "Rank1",
            BadgeNumber = null,
        };
        _dbContext.Users.Add(user);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rule.ExecuteAsync(_dbContext, TestContext.Current.CancellationToken)
        );
        Assert.Contains("Badge number is required", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ModifiedUserWithMissingBadgeNumber_ThrowsWhenRequired()
    {
        // Arrange
        var rule = CreateRule();

        var user = new User
        {
            Id = Guid.NewGuid(),
            IdirName = "testuser",
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            Gender = Gender.Female,
            Rank = "Rank1",
            BadgeNumber = "BADGE123",
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Modify to clear badge number
        user.BadgeNumber = null;
        _dbContext.Entry(user).State = EntityState.Modified;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rule.ExecuteAsync(_dbContext, TestContext.Current.CancellationToken)
        );
        Assert.Contains("Badge number is required", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateBadgeNumberInDatabase_Throws()
    {
        // Arrange
        var rule = CreateRule();

        // Add existing user to database
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            IdirName = "existinguser",
            FirstName = "Existing",
            LastName = "User",
            Email = "existing@example.com",
            Gender = Gender.Female,
            Rank = "Rank1",
            BadgeNumber = "BADGE123",
        };
        _dbContext.Users.Add(existingUser);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Try to create new user with duplicate badge
        var newUser = new User
        {
            Id = Guid.NewGuid(),
            IdirName = "newuser",
            FirstName = "New",
            LastName = "User",
            Email = "new@example.com",
            Gender = Gender.Female,
            Rank = "Rank1",
            BadgeNumber = "BADGE123",
        };
        _dbContext.Users.Add(newUser);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rule.ExecuteAsync(_dbContext, TestContext.Current.CancellationToken)
        );
        Assert.Contains("already in use", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateBadgeNumberWithinPendingChanges_Throws()
    {
        // Arrange
        var rule = CreateRule();

        var user1 = new User
        {
            Id = Guid.NewGuid(),
            IdirName = "user1",
            FirstName = "User",
            LastName = "One",
            Email = "user1@example.com",
            Gender = Gender.Female,
            Rank = "Rank1",
            BadgeNumber = "BADGE123",
        };

        var user2 = new User
        {
            Id = Guid.NewGuid(),
            IdirName = "user2",
            FirstName = "User",
            LastName = "Two",
            Email = "user2@example.com",
            Gender = Gender.Female,
            Rank = "Rank1",
            BadgeNumber = "badge123",
        };

        _dbContext.Users.AddRange(user1, user2);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rule.ExecuteAsync(_dbContext, TestContext.Current.CancellationToken)
        );
        Assert.Contains("pending changes", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ModifiedUserWithDuplicateBadgeNumber_Throws()
    {
        // Arrange
        var rule = CreateRule();

        // Add two existing users
        var user1 = new User
        {
            Id = Guid.NewGuid(),
            IdirName = "user1",
            FirstName = "User",
            LastName = "One",
            Email = "user1@example.com",
            Gender = Gender.Female,
            Rank = "Rank1",
            BadgeNumber = "BADGE001",
        };

        var user2 = new User
        {
            Id = Guid.NewGuid(),
            IdirName = "user2",
            FirstName = "User",
            LastName = "Two",
            Email = "user2@example.com",
            Gender = Gender.Female,
            Rank = "Rank1",
            BadgeNumber = "BADGE002",
        };

        _dbContext.Users.AddRange(user1, user2);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Modify user2 to have same badge as user1
        user2.BadgeNumber = "BADGE001";
        _dbContext.Entry(user2).State = EntityState.Modified;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rule.ExecuteAsync(_dbContext, TestContext.Current.CancellationToken)
        );
        Assert.Contains("already in use", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleDuplicateBadgeNumbersInDatabase_ThrowsCombinedMessage()
    {
        // Arrange
        var rule = CreateRule();

        var existingUser1 = new User
        {
            Id = Guid.NewGuid(),
            IdirName = "existing1",
            FirstName = "Existing",
            LastName = "One",
            Email = "existing1@example.com",
            Gender = Gender.Female,
            Rank = "Rank1",
            BadgeNumber = "BADGE001",
        };

        var existingUser2 = new User
        {
            Id = Guid.NewGuid(),
            IdirName = "existing2",
            FirstName = "Existing",
            LastName = "Two",
            Email = "existing2@example.com",
            Gender = Gender.Male,
            Rank = "Rank1",
            BadgeNumber = "BADGE002",
        };

        _dbContext.Users.AddRange(existingUser1, existingUser2);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var newUser1 = new User
        {
            Id = Guid.NewGuid(),
            IdirName = "new1",
            FirstName = "New",
            LastName = "One",
            Email = "new1@example.com",
            Gender = Gender.Female,
            Rank = "Rank1",
            BadgeNumber = "badge001",
        };

        var newUser2 = new User
        {
            Id = Guid.NewGuid(),
            IdirName = "new2",
            FirstName = "New",
            LastName = "Two",
            Email = "new2@example.com",
            Gender = Gender.Male,
            Rank = "Rank1",
            BadgeNumber = "BADGE002",
        };

        _dbContext.Users.AddRange(newUser1, newUser2);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rule.ExecuteAsync(_dbContext, TestContext.Current.CancellationToken)
        );
        Assert.Contains("'badge001'", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'BADGE002'", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("already in use", ex.Message);
    }

    private class FakeOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
    {
        public T CurrentValue => currentValue;

        public T Get(string? name) => currentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
