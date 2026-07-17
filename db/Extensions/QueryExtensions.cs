using Unified.Db.Models.Abstract;

namespace Unified.Db.Extensions;

public static class QueryExtensions
{
    /// <summary>
    /// Filters to only active (non-deleted) entities.
    /// An entity is considered inactive when either <see cref="ISoftDeletable.DeletedOn"/> or
    /// <see cref="ISoftDeletable.DeletedById"/> is set.
    /// </summary>
    public static IQueryable<T> WhereActive<T>(this IQueryable<T> query)
        where T : ISoftDeletable => query.Where(e => e.DeletedOn == null && e.DeletedById == null);

    /// <summary>
    /// Marks the entity as deleted by setting <see cref="ISoftDeletable.DeletedOn"/> to the current
    /// UTC time and <see cref="ISoftDeletable.DeletedById"/> to <paramref name="deletedById"/>.
    /// </summary>
    public static void SoftDelete(this ISoftDeletable entity, Guid deletedById)
    {
        entity.DeletedOn = DateTimeOffset.UtcNow;
        entity.DeletedById = deletedById;
    }
}
