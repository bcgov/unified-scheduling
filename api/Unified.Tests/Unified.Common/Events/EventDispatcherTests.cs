using Microsoft.Extensions.DependencyInjection;
using Unified.Common.Events;

namespace Unified.Tests.Common.Events;

public sealed class EventDispatcherTests
{
    [Fact]
    public async Task PublishAsync_WhenNoSideEffectsRegistered_CompletesAsNoOp()
    {
        var services = new ServiceCollection();
        services.AddScoped<IEventDispatcher, EventDispatcher>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<IEventDispatcher>();

        await dispatcher.PublishAsync(new TestSignal("created"), TestContext.Current.CancellationToken);
    }

    private sealed record TestSignal(string Name);
}
