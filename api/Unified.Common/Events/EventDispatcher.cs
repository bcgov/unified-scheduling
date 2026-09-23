using Microsoft.Extensions.DependencyInjection;

namespace Unified.Common.Events;

public sealed class EventDispatcher(IServiceProvider serviceProvider) : IEventDispatcher
{
    public async Task PublishAsync<TSignal>(TSignal signal, CancellationToken cancellationToken = default)
    {
        foreach (var sideEffect in serviceProvider.GetServices<IEntitySideEffect<TSignal>>())
        {
            await sideEffect.HandleAsync(signal, cancellationToken);
        }
    }
}
