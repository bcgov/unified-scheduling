using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

public interface IMigrationRunCoordinator
{
    Task<DataMigrationRun?> TryStartJobAsync(
        string job,
        bool dryRun,
        DateOnly cutover,
        CancellationToken cancellationToken,
        DateOnly? historyStartDate = null
    );
    Task<bool> RecoverStaleRunAsync(Guid runId, DateTimeOffset staleBefore, CancellationToken ct);
}
