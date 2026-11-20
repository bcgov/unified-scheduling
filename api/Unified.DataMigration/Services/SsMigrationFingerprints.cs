using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Unified.DataMigration.Sources;
using Unified.Db;

namespace Unified.DataMigration.Services;

// These are fingerprints of explicitly described projections/evidence, not whole databases.
// Length-prefixed UTF-8 fields avoid delimiter ambiguity. No input values are logged or stored.
internal static class SsMigrationFingerprints
{
    internal const string SourceFormat = "ss-source-projection-v1";
    internal const string OutcomeFormat = "ss-outcome-evidence-v1";
    internal const string CrosswalkFormat = "ss-applied-crosswalk-evidence-v1";
    internal const string SourcePrefix = SourceFormat + ":sha256:";
    internal const string MultipleSnapshots = SourceFormat + ":multiple-snapshots";

    internal sealed record Digest(string Format, string Algorithm, string Value, long Rows);

    internal static Digest Source(Dictionary<string, List<SsRow>> snapshot, CancellationToken ct)
    {
        using var hash = new FramedHash(SourceFormat);
        hash.Number(snapshot.Count);
        long count = 0;
        foreach (var table in snapshot.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            hash.Field(table.Key);
            hash.Number(table.Value.Count);
            string? previous = null;
            foreach (var row in table.Value.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                ct.ThrowIfCancellationRequested();
                if (previous == row.Id)
                    throw new InvalidOperationException("SS source snapshot contains duplicate keys.");
                previous = row.Id;
                hash.Field(row.Id);
                // SimulationInput sorts projected field names and replaces photo bytes with presence.
                hash.Bytes(JsonSerializer.SerializeToUtf8Bytes(row.SimulationInput(table.Key)));
                count++;
            }
        }
        return new(SourceFormat, "SHA-256", hash.Finish(), count);
    }

    internal static async Task<Digest> OutcomesAsync(UnifiedDbContext db, Guid runId, CancellationToken ct)
    {
        using var hash = new FramedHash(OutcomeFormat);
        var query = db.DataMigrationOutcomes.AsNoTracking().Where(x => x.RunId == runId);
        var ordered =
            db.Database.IsNpgsql()
                ? query
                    .OrderBy(x => EF.Functions.Collate(x.EntityType, "C"))
                    .ThenBy(x => EF.Functions.Collate(x.LegacyId, "C"))
            : db.Database.IsRelational()
                ? query
                    .OrderBy(x => EF.Functions.Collate(x.EntityType, "BINARY"))
                    .ThenBy(x => EF.Functions.Collate(x.LegacyId, "BINARY"))
            : query.OrderBy(x => x.EntityType).ThenBy(x => x.LegacyId);
        long count = 0;
        await foreach (var row in ordered.AsAsyncEnumerable().WithCancellation(ct))
        {
            hash.Field(row.EntityType);
            hash.Field(row.LegacyId);
            hash.Field(row.Outcome);
            hash.Field(row.Reason);
            count++;
        }
        hash.Number(count);
        return new(OutcomeFormat, "SHA-256", hash.Finish(), count);
    }

    internal static async Task<Digest> CrosswalksAsync(UnifiedDbContext db, Guid runId, CancellationToken ct)
    {
        using var hash = new FramedHash(CrosswalkFormat);
        // ExistingCrosswalk outcomes do not necessarily update LastAppliedRunId. They are
        // intentionally not claimed as applied crosswalks by this run.
        var query = db.DataMigrationRecords.AsNoTracking().Where(x => x.Source == "SS" && x.LastAppliedRunId == runId);
        var ordered =
            db.Database.IsNpgsql()
                ? query
                    .OrderBy(x => EF.Functions.Collate(x.EntityType, "C"))
                    .ThenBy(x => EF.Functions.Collate(x.LegacyId, "C"))
            : db.Database.IsRelational()
                ? query
                    .OrderBy(x => EF.Functions.Collate(x.EntityType, "BINARY"))
                    .ThenBy(x => EF.Functions.Collate(x.LegacyId, "BINARY"))
            : query.OrderBy(x => x.EntityType).ThenBy(x => x.LegacyId);
        long count = 0;
        await foreach (var row in ordered.AsAsyncEnumerable().WithCancellation(ct))
        {
            hash.Field(row.EntityType);
            hash.Field(row.LegacyId);
            hash.Field(row.TargetType);
            hash.Field(row.TargetKey);
            hash.Field(row.PayloadHash);
            hash.Field(row.AfterHash);
            hash.Field(row.IsDeleted ? "1" : "0");
            count++;
        }
        hash.Number(count);
        return new(CrosswalkFormat, "SHA-256", hash.Finish(), count);
    }

    private sealed class FramedHash : IDisposable
    {
        private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        internal FramedHash(string format) => Field(format);

        internal void Field(string? value) => Bytes(value is null ? null : Encoding.UTF8.GetBytes(value));

        internal void Bytes(byte[]? value)
        {
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(length, value?.Length ?? -1);
            hash.AppendData(length);
            if (value is not null)
                hash.AppendData(value);
        }

        internal void Number(long value)
        {
            Span<byte> bytes = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(bytes, value);
            hash.AppendData(bytes);
        }

        internal string Finish() => Convert.ToHexString(hash.GetHashAndReset());

        public void Dispose() => hash.Dispose();
    }
}
