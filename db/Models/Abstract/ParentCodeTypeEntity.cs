namespace Unified.Db.Models.Abstract;

public abstract class ParentCodeTypeEntity<TChildCodeType> : BaseCodeTypeEntity
    where TChildCodeType : BaseCodeTypeEntity
{
    public ICollection<TChildCodeType> ChildCodeTypes { get; } = [];
}