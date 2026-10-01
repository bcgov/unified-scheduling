using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

public sealed class DataMigrationControlService(UnifiedDbContext dbContext)
{
    public async Task<DataMigrationRun?> TryStartRunAsync(
        string source,
        bool dryRun,
        CancellationToken cancellationToken = default
    )
    {
        var run = new DataMigrationRun
        {
            Source = source,
            Status = "Running",
            StartedOn = DateTimeOffset.UtcNow,
            DryRun = dryRun,
        };

        dbContext.DataMigrationRuns.Add(run);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return run;
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(run).State = EntityState.Detached;
            return null;
        }
    }

    public async Task CompleteRunAsync(
        DataMigrationRun run,
        string status,
        CancellationToken cancellationToken = default
    )
    {
        run.Status = status;
        run.CompletedOn = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
