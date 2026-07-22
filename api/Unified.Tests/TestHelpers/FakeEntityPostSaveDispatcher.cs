using Unified.Common.PostSave;

namespace Unified.Tests.TestHelpers;

/// <summary>
/// A no-op dispatcher for use in unit tests. Handlers are never invoked.
/// </summary>
public sealed class FakeEntityPostSaveDispatcher : IEntityPostSaveDispatcher
{
    public Task DispatchCreateAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DispatchUpdateAsync<TEntity>(
        TEntity entity,
        TEntity previous,
        CancellationToken cancellationToken = default
    ) => Task.CompletedTask;
}
