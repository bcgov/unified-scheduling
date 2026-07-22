namespace Unified.Common.PostSave;

/// <summary>
/// Handles post-save side effects for a specific entity type after it is created.
/// Register implementations via DI and they will be automatically discovered and dispatched.
/// </summary>
public interface IEntityPostCreateHandler<TEntity>
{
    Task HandleAsync(TEntity entity, CancellationToken cancellationToken = default);
}
