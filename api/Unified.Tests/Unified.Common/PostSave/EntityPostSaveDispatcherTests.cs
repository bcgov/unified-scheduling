using Microsoft.Extensions.DependencyInjection;
using Unified.Common.PostSave;

namespace Unified.Tests.Unified.Common.PostSave;

public class EntityPostSaveDispatcherTests
{
    private sealed record SampleEntity(int Value);

    private sealed class CreateHandlerA : IEntityPostCreateHandler<SampleEntity>
    {
        public readonly List<SampleEntity> Seen = [];

        public Task HandleAsync(SampleEntity entity, CancellationToken cancellationToken = default)
        {
            Seen.Add(entity);
            return Task.CompletedTask;
        }
    }

    private sealed class CreateHandlerB : IEntityPostCreateHandler<SampleEntity>
    {
        public readonly List<SampleEntity> Seen = [];

        public Task HandleAsync(SampleEntity entity, CancellationToken cancellationToken = default)
        {
            Seen.Add(entity);
            return Task.CompletedTask;
        }
    }

    private sealed class UpdateHandlerA : IEntityPostUpdateHandler<SampleEntity>
    {
        public readonly List<(SampleEntity Current, SampleEntity Previous)> Seen = [];

        public Task HandleAsync(
            SampleEntity entity,
            SampleEntity previous,
            CancellationToken cancellationToken = default
        )
        {
            Seen.Add((entity, previous));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DispatchCreateAsync_InvokesAllRegisteredCreateHandlers()
    {
        var createHandlerA = new CreateHandlerA();
        var createHandlerB = new CreateHandlerB();

        var services = new ServiceCollection();
        services.AddSingleton<IEntityPostCreateHandler<SampleEntity>>(createHandlerA);
        services.AddSingleton<IEntityPostCreateHandler<SampleEntity>>(createHandlerB);

        await using var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new EntityPostSaveDispatcher(serviceProvider);
        var entity = new SampleEntity(123);

        await dispatcher.DispatchCreateAsync(entity, TestContext.Current.CancellationToken);

        Assert.Single(createHandlerA.Seen);
        Assert.Single(createHandlerB.Seen);
        Assert.Equal(entity, createHandlerA.Seen[0]);
        Assert.Equal(entity, createHandlerB.Seen[0]);
    }

    [Fact]
    public async Task DispatchUpdateAsync_InvokesAllRegisteredUpdateHandlersWithBothStates()
    {
        var updateHandler = new UpdateHandlerA();

        var services = new ServiceCollection();
        services.AddSingleton<IEntityPostUpdateHandler<SampleEntity>>(updateHandler);

        await using var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new EntityPostSaveDispatcher(serviceProvider);

        var previous = new SampleEntity(1);
        var current = new SampleEntity(2);

        await dispatcher.DispatchUpdateAsync(current, previous, TestContext.Current.CancellationToken);

        Assert.Single(updateHandler.Seen);
        Assert.Equal(current, updateHandler.Seen[0].Current);
        Assert.Equal(previous, updateHandler.Seen[0].Previous);
    }

    [Fact]
    public async Task DispatchMethods_WhenNoHandlersRegistered_CompletesWithoutError()
    {
        await using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var dispatcher = new EntityPostSaveDispatcher(serviceProvider);

        var previous = new SampleEntity(10);
        var current = new SampleEntity(11);

        await dispatcher.DispatchCreateAsync(current, TestContext.Current.CancellationToken);
        await dispatcher.DispatchUpdateAsync(current, previous, TestContext.Current.CancellationToken);
    }
}
