using Unified.Db.Models.Abstract;

namespace Unified.Common.Helpers.Extensions;

public static class QueryExtensions
{
    /// <summary>
    /// Filters to only active (non-deleted) entities.
    /// An entity is considered inactive when either <see cref="ISoftDeletable.DeletedOn"/> or
    /// <see cref="ISoftDeletable.DeletedById"/> is set.
    /// </summary>
    public static IQueryable<T> WhereActive<T>(this IQueryable<T> query)
        where T : ISoftDeletable =>
        query.Where(e => e.DeletedOn == null && e.DeletedById == null);
}
