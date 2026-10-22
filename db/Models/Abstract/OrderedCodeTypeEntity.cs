namespace Unified.Db.Models.Abstract;

/// <summary>
/// Base entity for code-table records with an explicit presentation order.
/// </summary>
public abstract class OrderedCodeTypeEntity : BaseCodeTypeEntity
{
    public int DisplayOrder { get; set; }
}
