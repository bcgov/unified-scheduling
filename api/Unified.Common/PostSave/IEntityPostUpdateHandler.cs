namespace Unified.Common.PostSave;

/// <summary>
/// Handles post-save side effects for a specific entity type after it is updated.
/// Receives both the current (post-save) state and a pre-mutation snapshot so handlers
/// can compare old vs new values to decide whether to act.
/// Register implementations via DI and they will be automatically discovered and dispatched.
/// </summary>
public interface IEntityPostUpdateHandler<TEntity>
{
    Task HandleAsync(TEntity entity, TEntity previous, CancellationToken cancellationToken = default);
}
