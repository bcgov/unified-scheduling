using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Unified.Db;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

public sealed class SsRollbackJournal(UnifiedDbContext db, IConfiguration configuration)
{
    public bool Enabled => configuration["DataMigrationRollback:EncryptionKey"] is not null;
    internal string Key
    {
        get
        {
            var value = configuration["DataMigrationRollback:EncryptionKey"];
            try
            {
                if (Convert.FromBase64String(value ?? "").Length == 32)
                    return value!;
            }
            catch (FormatException) { }
            throw new InvalidOperationException("SS rollback requires a secure 256-bit external encryption key.");
        }
    }
    internal Guid KeyId =>
        Guid.TryParse(configuration["DataMigrationRollback:KeyId"], out var id)
            ? id
            : throw new InvalidOperationException("SS rollback requires the external key identifier.");

    public async Task CaptureAsync(DataMigrationRun run, CancellationToken ct)
    {
        if (!Enabled)
            return; // Old, explicitly unrecoverable synthetic rehearsals remain identifiable.
        if (run.RollbackJournalKeyId is { } prior && prior != KeyId)
            throw new InvalidOperationException("SS rollback key changed during execution.");
        var key = Key;
        var id = KeyId;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('unified_ss.run',{run.Id.ToString()},true), set_config('unified_ss.key',{key},true), set_config('unified_ss.key_id',{id.ToString()},true)",
            ct
        );
        run.RollbackJournalKeyId = id;
        db.Entry(run).Property(x => x.RollbackJournalKeyId).IsModified = true;
        await db.SaveChangesAsync(ct);
    }
}
