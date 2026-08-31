using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Unified.Common.Audit;
using Unified.Db.Models.Abstract;

namespace Unified.Audit.Interceptors;

/// <summary>
/// Runs alongside Audit.NET's <c>AuditSaveChangesInterceptor</c> (registered immediately after
/// it, see <c>InterceptorRegistration</c>) to:
/// 1. Stamp <see cref="BaseEntity.CreatedById"/>/<see cref="BaseEntity.UpdatedById"/> before the
///    entity save executes.
/// 2. Own a database transaction spanning the entity save and the audit-record insert (which
///    happens against a separate <c>AuditRecordDbContext</c>/connection wrapper in
///    <see cref="AuditRecordDataProvider"/>), so a failure writing audit records rolls back the
///    entity changes too, and vice versa.
/// </summary>
public sealed class AuditTransactionInterceptor(ICurrentActorResolver actorResolver) : SaveChangesInterceptor
{
    private IDbContextTransaction? _ownedTransaction;

    // Sync SaveChanges bypasses this interceptor's async-only hooks (and Audit.NET's), which would
    // silently skip audit capture. Fail fast instead so misuse is caught immediately.
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        throw new NotSupportedException(
            "Synchronous SaveChanges is not supported when audit interceptors are registered. Use SaveChangesAsync instead."
        );
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        var context = eventData.Context;
        if (context is null)
        {
            return result;
        }

        StampActorFields(context);

        // Audit records are written to a separate AuditDbContext sharing this connection, so an
        // explicit ambient transaction is required to keep the entity save and the audit insert
        // atomic — without it, EF's implicit per-SaveChanges transaction would already have
        // committed the entity changes before the audit insert even runs.
        if (context.ChangeTracker.HasChanges() && context.Database.CurrentTransaction is null)
        {
            _ownedTransaction = await context.Database.BeginTransactionAsync(cancellationToken);
        }

        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default
    )
    {
        if (_ownedTransaction is not null)
        {
            await _ownedTransaction.CommitAsync(cancellationToken);
            await _ownedTransaction.DisposeAsync();
            _ownedTransaction = null;
        }

        return result;
    }

    public override async Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        if (_ownedTransaction is not null)
        {
            await _ownedTransaction.RollbackAsync(cancellationToken);
            await _ownedTransaction.DisposeAsync();
            _ownedTransaction = null;
        }
    }

    // Sets CurrentValue (not the CLR property) so the change is marked modified regardless of when DetectChanges last ran.
    private void StampActorFields(DbContext context)
    {
        var actorUserId = actorResolver.Resolve().ActorUserId;
        context.ChangeTracker.DetectChanges();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is not BaseEntity)
            {
                continue;
            }

            SetAuditUserField(entry, actorUserId);
        }
    }

    private static void SetAuditUserField(EntityEntry entry, Guid? actorUserId)
    {
        if (entry.State == EntityState.Added)
        {
            entry.Property(nameof(BaseEntity.CreatedById)).CurrentValue = actorUserId;
        }
        else if (entry.State == EntityState.Modified)
        {
            entry.Property(nameof(BaseEntity.UpdatedById)).CurrentValue = actorUserId;
        }
    }
}
