namespace Unified.Db.Models.Abstract;

/// <summary>
/// Base entity for code-table style records with lifecycle dates.
/// </summary>
public abstract class BaseCodeTypeEntity : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset EffectiveDate { get; set; }

    public DateTimeOffset? ExpiryDate { get; set; }
}
