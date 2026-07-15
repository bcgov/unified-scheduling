namespace Unified.Db.Models.Abstract;

public abstract class ChildCodeTypeEntity<TParentCodeType> : BaseCodeTypeEntity
    where TParentCodeType : BaseCodeTypeEntity
{
    public int ParentCodeTypeId { get; set; }

    public TParentCodeType ParentCodeType { get; set; } = null!;
}