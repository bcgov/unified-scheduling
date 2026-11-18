using Unified.Common.FeatureFlags;

namespace Unified.DataMigration.FeatureFlags;

public sealed class DataMigrationFeatureFlags : IFeatureFlags
{
    public const string Section = "FeatureFlags:DataMigration";

    public string Source => "DataMigration";

    public bool Enabled { get; set; }
}
