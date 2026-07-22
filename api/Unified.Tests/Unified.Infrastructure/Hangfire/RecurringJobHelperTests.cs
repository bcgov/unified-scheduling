using Hangfire;
using Hangfire.InMemory;
using Hangfire.Server;
using Unified.Common.Jobs;
using Unified.Infrastructure.Hangfire;

namespace Unified.Tests.Infrastructure.Hangfire;

public class RecurringJobHelperTests : IDisposable
{
    private readonly JobStorage? _previousStorage;

    public RecurringJobHelperTests()
    {
        // JobStorage.Current throws if it has never been initialized in this process,
        // so only capture it (to restore afterwards) when a value already exists.
        try
        {
            _previousStorage = JobStorage.Current;
        }
        catch (InvalidOperationException)
        {
            _previousStorage = null;
        }

        JobStorage.Current = new InMemoryStorage();
    }

    public void Dispose()
    {
        if (_previousStorage is not null)
        {
            JobStorage.Current = _previousStorage;
        }
    }

    private sealed class FakeRecurringJob(string jobName, string cronSchedule) : IRecurringJob
    {
        public string JobName => jobName;

        public string CronSchedule => cronSchedule;

        public Task Execute(PerformContext? context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public void AddOrUpdate_WhenCronScheduleValid_RegistersRecurringJob()
    {
        // Arrange
        var job = new FakeRecurringJob("test-job", "0 0 * * *");

        // Act
        RecurringJobHelper.AddOrUpdate(job);

        // Assert
        using var connection = JobStorage.Current.GetConnection();
        var recurringJobIds = connection.GetAllItemsFromSet("recurring-jobs");
        Assert.Contains("test-job", recurringJobIds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("disabled")]
    [InlineData("DISABLE")]
    public void AddOrUpdate_WhenCronScheduleDisabled_RemovesRecurringJob(string cronSchedule)
    {
        // Arrange
        var enabledJob = new FakeRecurringJob("disabled-job", "0 0 * * *");
        RecurringJobHelper.AddOrUpdate(enabledJob);

        var disabledJob = new FakeRecurringJob("disabled-job", cronSchedule);

        // Act
        RecurringJobHelper.AddOrUpdate(disabledJob);

        // Assert
        using var connection = JobStorage.Current.GetConnection();
        var recurringJobIds = connection.GetAllItemsFromSet("recurring-jobs");
        Assert.DoesNotContain("disabled-job", recurringJobIds);
    }
}
