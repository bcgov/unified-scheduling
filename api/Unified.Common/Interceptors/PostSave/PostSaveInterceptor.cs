using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Unified.Common.PostSave;

namespace Unified.Common.Interceptors;

/// <summary>
/// Captures changes with registered handlers before save; dispatches only after the initial EF save succeeds.
/// UnifiedDbContext owns the transaction around the initial save, handler saves, and auditing.
/// </summary>
public sealed class PostSaveInterceptor : SaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider;
    private readonly ILookup<(Type, SaveAction), IPostSaveHandler> _handlers;
    private readonly ConditionalWeakTable<DbContext, SaveState> _states = new();

    public PostSaveInterceptor(IEnumerable<IPostSaveHandler> handlers, TimeProvider? timeProvider = null)
    {
        _handlers = handlers.ToLookup(handler => (handler.EntityType, handler.Action));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        if (eventData.Context is not { } db)
            return new(result);

        var state = _states.GetValue(db, _ => new SaveState());
        if (state.Dispatching)
            return new(result);

        state.Pending.Clear();
        if (result.HasResult || _handlers.Count == 0)
            return new(result);

        var timestamp = _timeProvider.GetUtcNow();
        foreach (var entry in db.ChangeTracker.Entries())
        {
            SaveAction? action = entry.State switch
            {
                EntityState.Added => SaveAction.Create,
                EntityState.Modified => SaveAction.Update,
                EntityState.Deleted => SaveAction.Delete,
                _ => null,
            };
            if (action is null)
                continue;

            // EF metadata gives the actual model type even when the instance is a proxy.
            foreach (var handler in _handlers[(entry.Metadata.ClrType, action.Value)])
                state.Pending.Add(new PendingSave(new SaveContext(entry.Entity, action.Value, timestamp), handler));
        }

        return new(result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default
    )
    {
        if (
            eventData.Context is not { } db
            || !_states.TryGetValue(db, out var state)
            || state.Dispatching
            || state.Pending.Count == 0
        )
            return result;

        var pending = state.Pending.ToArray();
        state.Pending.Clear();

        // A follow-up save with unaccepted original changes would replay their inserts/deletes.
        if (
            pending.Any(save =>
                db.Entry(save.Context.Entity).State is EntityState.Added or EntityState.Modified or EntityState.Deleted
            )
        )
            throw new InvalidOperationException(
                "Post-save handlers require SaveChangesAsync with acceptAllChangesOnSuccess enabled."
            );

        if (db.Database.IsRelational() && db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Post-save handlers require a transaction enclosing SaveChangesAsync.");

        state.Dispatching = true;
        try
        {
            foreach (var save in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await save.Handler.HandleAsync(db, save.Context, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (db.ChangeTracker.HasChanges())
                await db.SaveChangesAsync(cancellationToken);

            // Preserve EF's original row count, rather than including nested business/audit saves.
            return result;
        }
        finally
        {
            state.Dispatching = false;
            state.Pending.Clear();
        }
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        ClearPending(eventData.Context);
        return Task.CompletedTask;
    }

    public override Task SaveChangesCanceledAsync(
        DbContextEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        ClearPending(eventData.Context);
        return Task.CompletedTask;
    }

    private void ClearPending(DbContext? db)
    {
        if (db is not null && _states.TryGetValue(db, out var state))
            state.Pending.Clear();
    }

    private sealed class SaveState
    {
        public bool Dispatching;
        public List<PendingSave> Pending { get; } = [];
    }

    private sealed record PendingSave(SaveContext Context, IPostSaveHandler Handler);
}