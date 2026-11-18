namespace Unified.Db.Models.DataMigration;

public sealed class DataMigrationCheckpoint
{
    public long Id { get; set; }
    public string Source { get; set; } = "SS";
    public required string Job { get; set; }
    public required string Stream { get; set; }
    public required string EntityType { get; set; }
    public required string LegacyId { get; set; }
    public required string IdKind { get; set; }
    public DateTimeOffset SourceChangedAtUtc { get; set; }
    public required string UpperLegacyId { get; set; }
    public DateTimeOffset UpperChangedAtUtc { get; set; }
    public Guid LastRunId { get; set; }
    public long CommittedBatches { get; set; }
    public DateOnly HistoryStartDate { get; set; }
}
