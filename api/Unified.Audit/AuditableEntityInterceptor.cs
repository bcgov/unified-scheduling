using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Unified.Db.Models.Abstract;

namespace Unified.Audit;

/// <summary>
/// Stamps <see cref="BaseEntity"/> audit columns before EF writes them, using the same
/// <see cref="ICurrentActorResolver"/> the audit trail uses.
///
/// <c>CreatedOn</c> remains database-generated. Modified rows receive a UTC <c>UpdatedOn</c>
/// unless the caller explicitly changed it to a non-null value.
/// </summary>
public sealed class AuditableEntityInterceptor(ICurrentActorResolver actorResolver, TimeProvider? timeProvider = null)
    : SaveChangesInterceptor
{
    private static readonly ConditionalWeakTable<BaseEntity, PreviousUpdatedOn> PreviousUpdatedOnValues = new();
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    internal static void CapturePreviousUpdatedOn(BaseEntity entity, DateTimeOffset? updatedOn)
    {
        lock (PreviousUpdatedOnValues)
        {
            PreviousUpdatedOnValues.Remove(entity);
            PreviousUpdatedOnValues.Add(entity, new(updatedOn));
        }
    }

    internal static bool TryTakePreviousUpdatedOn(BaseEntity entity, out DateTimeOffset? updatedOn)
    {
        lock (PreviousUpdatedOnValues)
        {
            if (PreviousUpdatedOnValues.TryGetValue(entity, out var previousUpdatedOn))
            {
                PreviousUpdatedOnValues.Remove(entity);
                updatedOn = previousUpdatedOn.Value;
                return true;
            }
        }

        updatedOn = null;
        return false;
    }

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
                var updatedOn = entry.Property(entity => entity.UpdatedOn);
                CapturePreviousUpdatedOn(entry.Entity, updatedOn.OriginalValue);
                if (
                    !updatedOn.IsModified
                    || updatedOn.CurrentValue is null
                    || updatedOn.CurrentValue == updatedOn.OriginalValue
                )
                {
                    updatedOn.CurrentValue = _timeProvider.GetUtcNow();
                }
            }
        }
    }

    private sealed record PreviousUpdatedOn(DateTimeOffset? Value);
}
