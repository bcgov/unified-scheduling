using Unified.DataMigration.Sources;

namespace Unified.DataMigration.Services;

// Host-supplied adapter to actual application queries, not duplicated business formulas.
public interface ISsMigrationBusinessValidator
{
    Task ValidateAsync(string job, Dictionary<string, List<SsRow>> rows, SsWriteSession session, CancellationToken ct);
}
