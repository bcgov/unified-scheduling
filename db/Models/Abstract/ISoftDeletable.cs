namespace Unified.Db.Models.Abstract;

/// <summary>
/// Marks an entity as soft-deletable.
/// An entity is considered inactive when either <see cref="DeletedOn"/> or
/// <see cref="DeletedById"/> is set.
/// </summary>
public interface ISoftDeletable
{
    DateTimeOffset? DeletedOn { get; set; }
    Guid? DeletedById { get; set; }
}
