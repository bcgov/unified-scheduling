using Microsoft.EntityFrameworkCore;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Extensions;

public static class ConcurrencyExtensions
{
    /// <summary>
    /// Checks a client version and makes this tracked root participate in the next save,
    /// including edits that only change related records. Save/transaction ownership stays
    /// with the caller; EF accepts the returned xmin after each successful save.
    /// </summary>
    public static void RequireVersion<T>(this UnifiedDbContext db, T entity, uint expectedVersion)
        where T : BaseEntity
    {
        var entry = db.Entry(entity);
        if (entry.State is not (EntityState.Unchanged or EntityState.Modified))
            throw new InvalidOperationException("Version checks require an existing tracked entity.");

        if (entity.ConcurrencyToken != expectedVersion)
            throw new DbUpdateConcurrencyException("The supplied record version is stale.");

        entry.Property(item => item.ConcurrencyToken).OriginalValue = expectedVersion;
        var updatedOn = entry.Property(item => item.UpdatedOn);
        updatedOn.CurrentValue = DateTimeOffset.UtcNow;
        updatedOn.IsModified = true;
    }
}
