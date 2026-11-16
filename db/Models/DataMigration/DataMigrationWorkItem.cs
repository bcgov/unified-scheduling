namespace Unified.Db.Models.DataMigration;

// Durable retry ledger. Never store source payloads, notes, photos or credentials here.
public sealed class DataMigrationWorkItem
{
    public long Id { get; set; }
    public string Source { get; set; } = "SS";
    public required string Job { get; set; }
    public required string Stream { get; set; }
    public required string EntityType { get; set; }
    public required string LegacyId { get; set; }
    public required string Outcome { get; set; }
    public string? Reason { get; set; }
    public bool PendingRetry { get; set; }
    public int Attempts { get; set; }
    public Guid LastRunId { get; set; }
    public DateTimeOffset? SourceChangedAtUtc { get; set; }
}
