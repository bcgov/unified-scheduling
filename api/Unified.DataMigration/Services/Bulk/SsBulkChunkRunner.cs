using Microsoft.EntityFrameworkCore;
using Npgsql;
using Unified.Db;

namespace Unified.DataMigration.Services.Bulk;

/// <summary>
/// Runs one bulk chunk as ONE transaction: fence (run still Running, no pause/abort control) →
/// chunk body (staging + set-based writes) → fence again → ledger flush (outcomes, work items,
/// checkpoints, counters, heartbeat) → optional test hook → commit. Cancellation and controls are
/// observed between chunks; a failure rolls back the whole chunk and discards its ledger changes.
/// </summary>
internal sealed class SsBulkChunkRunner(
    UnifiedDbContext db,
    SsBulkLedger ledger,
    Func<CancellationToken, Task> fence,
    Func<int, CancellationToken, Task>? beforeCommit = null
)
{
    private int chunk;

    internal int Committed { get; private set; }

    internal async Task RunAsync(
        Func<NpgsqlConnection, CancellationToken, Task> body,
        CancellationToken ct,
        bool final = false
    )
    {
        ct.ThrowIfCancellationRequested();
        var index = chunk++;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await fence(ct);
            db.ChangeTracker.Clear();
            await body((NpgsqlConnection)db.Database.GetDbConnection(), ct);
            db.ChangeTracker.Clear();
            await fence(ct);
            await ledger.FlushAsync(final, ct);
            if (beforeCommit is not null)
                await beforeCommit(index, ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            ledger.Discard();
            db.ChangeTracker.Clear();
            throw;
        }
        ledger.Accept();
        Committed++;
    }
}
