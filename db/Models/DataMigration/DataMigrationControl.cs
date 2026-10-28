namespace Unified.Db.Models.DataMigration;

public sealed class DataMigrationControl
{
    public required string Source { get; set; }
    public bool IsPaused { get; set; }
    public DateTimeOffset? AbortRequestedOn { get; set; }
    public DateTimeOffset ChangedOn { get; set; }
    public string? ChangedBy { get; set; }
}
