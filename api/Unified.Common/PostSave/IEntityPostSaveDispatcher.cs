namespace Unified.Common.PostSave;

/// <summary>
/// Dispatches post-save handlers registered for a given entity type.
/// </summary>
public interface IEntityPostSaveDispatcher
{
    Task DispatchCreateAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default);

    Task DispatchUpdateAsync<TEntity>(TEntity entity, TEntity previous, CancellationToken cancellationToken = default);
}
