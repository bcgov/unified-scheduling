using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

public sealed class DataMigrationOrchestrator(UnifiedDbContext dbContext)
{
    public static string ComputePayloadHash(string payload) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

    public static bool HasPayloadChanged(DataMigrationRecord? record, string payloadHash) =>
        record is null || !string.Equals(record.PayloadHash, payloadHash, StringComparison.Ordinal);

    public static bool IsAfterWatermark(
        DateTimeOffset changedAtUtc,
        string legacyId,
        DataMigrationWatermark? watermark
    ) =>
        watermark is null
        || changedAtUtc > watermark.SourceChangedAtUtc
        || (changedAtUtc == watermark.SourceChangedAtUtc && string.CompareOrdinal(legacyId, watermark.LegacyId) > 0);

    public async Task ExecuteBatchAsync(
        Func<CancellationToken, Task> writeBatch,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await writeBatch(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task RecordFailureAsync(DataMigrationFailure failure, CancellationToken cancellationToken = default)
    {
        dbContext.DataMigrationFailures.Add(failure);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
