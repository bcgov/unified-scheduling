namespace Unified.Db.Models.DataMigration;

public sealed class DataMigrationWatermark
{
    public long Id { get; set; }
    public required string Source { get; set; }
    public required string EntityType { get; set; }
    public DateTimeOffset SourceChangedAtUtc { get; set; }
    public required string LegacyId { get; set; }
    public Guid? LastCompletedRunId { get; set; }
    public DataMigrationRun? LastCompletedRun { get; set; }
}
