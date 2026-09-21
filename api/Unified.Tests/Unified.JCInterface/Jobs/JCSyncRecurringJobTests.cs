using Microsoft.Extensions.Options;
using Unified.JCInterface.Jobs;
using Unified.JCInterface.Options;

namespace Unified.Tests.JCInterface.Jobs;

public class JCSyncRecurringJobTests
{
    [Fact]
    public void JobName_IsStable()
    {
        var job = CreateJob("0 2 * * *");

        Assert.Equal("jc-interface-sync", job.JobName);
    }

    [Theory]
    [InlineData("0 2 * * *")]
    [InlineData("disabled")]
    public void CronSchedule_UsesConfiguredSyncCron(string syncCron)
    {
        var job = CreateJob(syncCron);

        Assert.Equal(syncCron, job.CronSchedule);
    }

    private static JCSyncRecurringJob CreateJob(string syncCron) =>
        new(null!, Options.Create(new JCInterfaceOptions { SyncCron = syncCron }));
}