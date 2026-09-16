using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Unified.Common.Interceptors.PostSave;

/// <summary>
/// Captures changes with registered handlers before save; dispatches only after the initial EF save succeeds.
/// UnifiedDbContext owns the transaction around the initial save, handler saves, and auditing.
/// </summary>
public sealed class PostSaveInterceptor(IEnumerable<IPostSaveHandler> handlers, TimeProvider? timeProvider = null)
    : SaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ILookup<(Type, SaveAction), IPostSaveHandler> _handlers = handlers.ToLookup(handler =>
        (handler.EntityType, handler.Action)
    );
    private readonly ConditionalWeakTable<DbContext, SaveState> _states = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        if (eventData.Context is not { } db || result.HasResult || _handlers.Count == 0)
            return new(result);

        var state = _states.GetValue(db, _ => new SaveState());
        if (state.Dispatching)
            return new(result);

        state.Pending.Clear();
        state.SavedEntities.Clear();

        var timestamp = _timeProvider.GetUtcNow();
        foreach (var entry in db.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            state.SavedEntities.Add(entry.Entity);

            var action = entry.State switch
            {
                EntityState.Added => SaveAction.Create,
                EntityState.Modified => SaveAction.Update,
                _ => SaveAction.Delete,
            };

            // EF metadata gives the actual model type even when the instance is a proxy.
            foreach (var handler in _handlers[(entry.Metadata.ClrType, action)])
                state.Pending.Add(new PendingSave(new SaveContext(entry.Entity, action, timestamp), handler));
        }

        return new(result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default
    )
    {
        if (eventData.Context is not { } db)
            return result;

        if (!_states.TryGetValue(db, out var state) || state.Dispatching || state.Pending.Count == 0)
            return result;

        var pending = state.Pending.ToArray();
        state.Pending.Clear();

        state.Dispatching = true;
        try
        {
            var statesBeforeHandlers = db
                .ChangeTracker.Entries()
                .Where(entry => state.SavedEntities.Contains(entry.Entity))
                .ToDictionary(entry => entry.Entity, entry => entry.State, ReferenceEqualityComparer.Instance);

            foreach (var save in pending)
                await save.Handler.HandleAsync(db, save.Context, cancellationToken);

            var changedEntries = db
                .ChangeTracker.Entries()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .ToArray();

            var hasFollowUpChanges = changedEntries.Any(entry =>
                !state.SavedEntities.Contains(entry.Entity)
                || !statesBeforeHandlers.TryGetValue(entry.Entity, out var stateBeforeHandler)
                || stateBeforeHandler != entry.State
            );

            if (hasFollowUpChanges)
            {
                var suppressedEntries = changedEntries
                    .Where(entry =>
                        state.SavedEntities.Contains(entry.Entity)
                        && statesBeforeHandlers.TryGetValue(entry.Entity, out var stateBeforeHandler)
                        && stateBeforeHandler == entry.State
                    )
                    .Select(entry => new SuppressedEntry(entry, entry.State))
                    .ToArray();

                foreach (var suppressed in suppressedEntries)
                    suppressed.Entry.State = EntityState.Unchanged;

                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                finally
                {
                    foreach (var suppressed in suppressedEntries)
                        suppressed.Entry.State = suppressed.State;
                }
            }

            // Preserve EF's original row count, rather than including nested business/audit saves.
            return result;
        }
        finally
        {
            state.Dispatching = false;
            state.Pending.Clear();
            state.SavedEntities.Clear();
        }
    }

    private sealed class SaveState
    {
        public bool Dispatching;
        public List<PendingSave> Pending { get; } = [];
        public HashSet<object> SavedEntities { get; } = new(ReferenceEqualityComparer.Instance);
    }

    private sealed record PendingSave(SaveContext Context, IPostSaveHandler Handler);

    private sealed record SuppressedEntry(EntityEntry Entry, EntityState State);
}
