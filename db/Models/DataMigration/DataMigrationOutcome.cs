namespace Unified.Db.Models.DataMigration;

// Only source identifiers and bounded machine-readable codes belong here. Never source text.
public sealed class DataMigrationOutcome
{
    public long Id { get; set; }
    public Guid RunId { get; set; }
    public required string EntityType { get; set; }
    public required string LegacyId { get; set; }
    public required string Outcome { get; set; }
    public string? Reason { get; set; }
}
