namespace Unified.Db.Models.DataMigration;

public sealed class DataMigrationFailure
{
    public long Id { get; set; }
    public Guid RunId { get; set; }
    public required string Source { get; set; }
    public string? EntityType { get; set; }
    public string? LegacyId { get; set; }
    public required string ErrorType { get; set; }
    public required string ErrorMessage { get; set; }
    public string? Watermark { get; set; }
    public DateTimeOffset OccurredOn { get; set; }
    public DataMigrationRun? Run { get; set; }
}
