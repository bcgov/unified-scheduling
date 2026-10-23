namespace Unified.Db.Models.DataMigration;

public sealed class DataMigrationMutation
{
    public long Id { get; set; }
    public Guid RunId { get; set; }
    public long DataMigrationRecordId { get; set; }
    public long Sequence { get; set; }
    public required string Operation { get; set; }
    public string? BeforeImage { get; set; }
    public required string AfterHash { get; set; }
    public string? Version { get; set; }
    public DataMigrationRun? Run { get; set; }
    public DataMigrationRecord? DataMigrationRecord { get; set; }
}
