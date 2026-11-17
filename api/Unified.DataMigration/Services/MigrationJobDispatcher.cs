using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace Unified.DataMigration.Services;

public interface IMigrationJobDispatcher
{
    void Validate(string job, bool dryRun);
    void Enqueue(Guid runId);
}

public sealed class MigrationJobDispatcher(SsMigrationExecutor executor, IServiceProvider services)
    : IMigrationJobDispatcher
{
    public void Validate(string job, bool dryRun) => executor.ValidateRequest(job, dryRun);

    public void Enqueue(Guid runId) =>
        services
            .GetRequiredService<IBackgroundJobClient>()
            .Enqueue<SsMigrationExecutor>(x => x.ExecuteAcquiredAsync(runId, CancellationToken.None));
}
