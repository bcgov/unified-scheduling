namespace Unified.DataMigration.FeatureFlags;

public sealed class DataMigrationFeatureFlags
{
    public const string Section = "FeatureFlags:DataMigration";

    public bool Enabled { get; set; }
}
