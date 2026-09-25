namespace Unified.Common.Events;

/// <summary>
/// Handles in-process side effects for a published signal.
/// </summary>
/// <typeparam name="TSignal">The signal type.</typeparam>
public interface IEntitySideEffect<in TSignal>
{
    Task HandleAsync(TSignal signal, CancellationToken cancellationToken);
}
