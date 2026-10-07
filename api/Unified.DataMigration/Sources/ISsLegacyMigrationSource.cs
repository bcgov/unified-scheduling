namespace Unified.DataMigration.Sources;

public interface ISsLegacyMigrationSource : ILegacyMigrationSource
{
    Task<SsSchemaPreflightResult> PreflightAsync(CancellationToken cancellationToken = default);
}
