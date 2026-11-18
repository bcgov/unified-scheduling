namespace Unified.Db.Models.DataMigration;

public sealed class DataMigrationJournal
{
    public long Id { get; set; }
    public Guid RunId { get; set; }
    public Guid KeyId { get; set; }
    public required string SchemaName { get; set; }
    public required string TableName { get; set; }
    public required string RowKey { get; set; }
    public required string Operation { get; set; }

    // PGP AES256 ciphertext. Photo bytes are never journaled or hashed.
    public byte[]? BeforeImage { get; set; }
    public required string AfterHash { get; set; }
    public required string AfterVersion { get; set; }
    public string[] ChangedColumns { get; set; } = [];
    public bool BeforeHadPhoto { get; set; }
    public bool PhotoChanged { get; set; }
}
