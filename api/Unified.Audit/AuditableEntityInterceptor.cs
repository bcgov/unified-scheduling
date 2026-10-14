using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Unified.Db.Models.Abstract;

namespace Unified.Audit;

/// <summary>
/// Stamps <see cref="BaseEntity"/> actor columns before EF writes them, using the same
/// <see cref="ICurrentActorResolver"/> the audit trail uses.
///
/// Timestamps remain database-generated; this interceptor only populates <c>CreatedById</c> and
/// <c>UpdatedById</c>.
/// </summary>
public sealed class AuditableEntityInterceptor(ICurrentActorResolver actorResolver) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        Stamp(eventData.Context);
        return new(result);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        CurrentActor? actor = null;

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            // Resolved lazily so saves with no BaseEntity changes - notably the nested AuditRecord
            // insert Audit.NET performs after a successful save - never invoke the actor resolver.
            actor ??= actorResolver.Resolve();
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedById ??= actor.ActorUserId;
            }
            else
            {
                entry.Entity.UpdatedById = actor.ActorUserId;
            }
        }
    }
}
