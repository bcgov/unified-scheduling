namespace Unified.Common.Events;

/// <summary>
/// Publishes in-process signals to registered side-effect handlers.
/// </summary>
public interface IEventDispatcher
{
    Task PublishAsync<TSignal>(TSignal signal, CancellationToken cancellationToken = default);
}
