using Microsoft.Extensions.DependencyInjection;

namespace Unified.Common.PostSave;

public sealed class EntityPostSaveDispatcher(IServiceProvider serviceProvider) : IEntityPostSaveDispatcher
{
    public async Task DispatchCreateAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default)
    {
        var handlers = serviceProvider.GetServices<IEntityPostCreateHandler<TEntity>>();

        foreach (var handler in handlers)
        {
            await handler.HandleAsync(entity, cancellationToken);
        }
    }

    public async Task DispatchUpdateAsync<TEntity>(
        TEntity entity,
        TEntity previous,
        CancellationToken cancellationToken = default
    )
    {
        var handlers = serviceProvider.GetServices<IEntityPostUpdateHandler<TEntity>>();

        foreach (var handler in handlers)
        {
            await handler.HandleAsync(entity, previous, cancellationToken);
        }
    }
}
