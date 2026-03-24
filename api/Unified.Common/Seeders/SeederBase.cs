using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Unified.Common.Seeding;

/// <summary>
/// Base class for module seeders. Implements the public seed flow and delegates seeding logic.
/// </summary>
public abstract class SeederBase<TContext>(ILogger logger)
    where TContext : DbContext
{
    public ILogger Logger { get; } = logger;

    public abstract int Order { get; }

    public virtual string Name => GetType().Name;

    protected abstract Task ExecuteAsync(TContext dbContext, CancellationToken cancellationToken);

    public Task SeedAsync(TContext dbContext, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(dbContext, cancellationToken);
    }
}
