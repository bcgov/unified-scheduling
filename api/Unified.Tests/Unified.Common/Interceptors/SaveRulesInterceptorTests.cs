using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Unified.Common.Interceptors;
using Unified.Db;
using Unified.Db.Models.UserManagement;
using Xunit;

namespace Unified.Tests.Unified.Common.Interceptors;

public class SaveRulesInterceptorTests
{
    private readonly DbContextOptions<UnifiedDbContext> _options;

    public SaveRulesInterceptorTests()
    {
        _options = new DbContextOptionsBuilder<UnifiedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task SavingChangesAsync_NoRules_ProceedsNormally()
    {
        // Arrange
        var rules = new List<ISaveRule>();
        var logger = new FakeLogger<SaveRulesInterceptor>();
        var interceptor = new SaveRulesInterceptor(rules, logger);

        var eventData = CreateEventData();
        var result = default(InterceptionResult<int>);

        // Act
        var returnResult = await interceptor.SavingChangesAsync(
            eventData,
            result,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(result, returnResult);
    }

    [Fact]
    public async Task SavingChangesAsync_SingleRuleExecutesSuccessfully_ReturnsResult()
    {
        // Arrange
        var rule = new FakeSuccessRule();
        var rules = new List<ISaveRule> { rule };
        var logger = new FakeLogger<SaveRulesInterceptor>();
        var interceptor = new SaveRulesInterceptor(rules, logger);

        var eventData = CreateEventData();
        var result = default(InterceptionResult<int>);

        // Act
        var returnResult = await interceptor.SavingChangesAsync(
            eventData,
            result,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(rule.ExecuteCalled);
        Assert.Equal(result, returnResult);
    }

    [Fact]
    public async Task SavingChangesAsync_MultipleRulesExecuteInOrder()
    {
        // Arrange
        var executionOrder = new List<int>();
        var rule1 = new FakeOrderedRule(1, executionOrder);
        var rule2 = new FakeOrderedRule(2, executionOrder);
        var rule3 = new FakeOrderedRule(3, executionOrder);
        var rules = new List<ISaveRule> { rule1, rule2, rule3 };
        var logger = new FakeLogger<SaveRulesInterceptor>();
        var interceptor = new SaveRulesInterceptor(rules, logger);

        var eventData = CreateEventData();
        var result = default(InterceptionResult<int>);

        // Act
        await interceptor.SavingChangesAsync(eventData, result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, executionOrder);
    }

    [Fact]
    public async Task SavingChangesAsync_RuleThrows_ExceptionPropagates()
    {
        // Arrange
        var rule = new FakeFailureRule("Test error");
        var rules = new List<ISaveRule> { rule };
        var logger = new FakeLogger<SaveRulesInterceptor>();
        var interceptor = new SaveRulesInterceptor(rules, logger);

        var eventData = CreateEventData();
        var result = default(InterceptionResult<int>);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            interceptor.SavingChangesAsync(eventData, result, TestContext.Current.CancellationToken).AsTask()
        );
        Assert.Equal("Test error", ex.Message);
    }

    [Fact]
    public async Task SavingChangesAsync_FirstRuleThrows_SecondRuleDoesNotRun()
    {
        // Arrange
        var rule1 = new FakeFailureRule("Rule 1 failed");
        var rule2 = new FakeSuccessRule();
        var rules = new List<ISaveRule> { rule1, rule2 };
        var logger = new FakeLogger<SaveRulesInterceptor>();
        var interceptor = new SaveRulesInterceptor(rules, logger);

        var eventData = CreateEventData();
        var result = default(InterceptionResult<int>);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            interceptor.SavingChangesAsync(eventData, result, TestContext.Current.CancellationToken).AsTask()
        );
        Assert.False(rule2.ExecuteCalled, "Second rule should not have executed");
    }

    [Fact]
    public async Task SavingChangesAsync_NullContext_ReturnsResultWithoutRunningRules()
    {
        // Arrange
        var rule = new FakeSuccessRule();
        var rules = new List<ISaveRule> { rule };
        var logger = new FakeLogger<SaveRulesInterceptor>();
        var interceptor = new SaveRulesInterceptor(rules, logger);

        // Create event data with null context
        var eventData = new DbContextEventData(null, null, null);
        var result = default(InterceptionResult<int>);

        // Act
        var returnResult = await interceptor.SavingChangesAsync(
            eventData,
            result,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(rule.ExecuteCalled);
        Assert.Equal(result, returnResult);
    }

    [Fact]
    public async Task SavingChangesAsync_LogsRuleExecution()
    {
        // Arrange
        var rule = new FakeSuccessRule();
        var rules = new List<ISaveRule> { rule };
        var logger = new FakeLogger<SaveRulesInterceptor>();
        var interceptor = new SaveRulesInterceptor(rules, logger);

        var eventData = CreateEventData();
        var result = default(InterceptionResult<int>);

        // Act
        await interceptor.SavingChangesAsync(eventData, result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Running rule:", logger.DebugMessages.FirstOrDefault() ?? "");
        Assert.Contains("FakeSuccessRule", logger.DebugMessages.FirstOrDefault() ?? "");
    }

    [Fact]
    public async Task SavingChangesAsync_LogsRuleFailure()
    {
        // Arrange
        var rule = new FakeFailureRule("Test failure");
        var rules = new List<ISaveRule> { rule };
        var logger = new FakeLogger<SaveRulesInterceptor>();
        var interceptor = new SaveRulesInterceptor(rules, logger);

        var eventData = CreateEventData();
        var result = default(InterceptionResult<int>);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            interceptor.SavingChangesAsync(eventData, result, TestContext.Current.CancellationToken).AsTask()
        );
        Assert.True(logger.ErrorLogged, "Error should be logged");
        Assert.Contains("failed", logger.ErrorMessages.FirstOrDefault() ?? "");
    }

    // Helper methods and fakes

    private DbContextEventData CreateEventData()
    {
        var context = new UnifiedDbContext(_options);
        // SaveRulesInterceptor skips entirely when there are no non-audit tracked changes.
        context.Add(new User());
        return new DbContextEventData(null, null, context);
    }

    private class FakeSuccessRule : ISaveRule
    {
        public bool ExecuteCalled { get; private set; }

        public Task ExecuteAsync(DbContext context, CancellationToken cancellationToken)
        {
            ExecuteCalled = true;
            return Task.CompletedTask;
        }
    }

    private class FakeFailureRule : ISaveRule
    {
        private readonly string _errorMessage;

        public FakeFailureRule(string errorMessage)
        {
            _errorMessage = errorMessage;
        }

        public Task ExecuteAsync(DbContext context, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(_errorMessage);
        }
    }

    private class FakeOrderedRule : ISaveRule
    {
        private readonly int _order;
        private readonly List<int> _executionOrder;

        public FakeOrderedRule(int order, List<int> executionOrder)
        {
            _order = order;
            _executionOrder = executionOrder;
        }

        public Task ExecuteAsync(DbContext context, CancellationToken cancellationToken)
        {
            _executionOrder.Add(_order);
            return Task.CompletedTask;
        }
    }

    private class FakeLogger<T> : ILogger<T>
    {
        public List<string> DebugMessages { get; } = [];
        public List<string> ErrorMessages { get; } = [];
        public bool ErrorLogged { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            var message = formatter(state, exception);

            if (logLevel == LogLevel.Debug)
            {
                DebugMessages.Add(message);
            }
            else if (logLevel == LogLevel.Error)
            {
                ErrorMessages.Add(message);
                ErrorLogged = true;
            }
        }
    }
}
