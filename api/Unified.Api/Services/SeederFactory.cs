using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;

namespace Unified.Api.Services;

public class SeederFactory<TContext>
    where TContext : DbContext
{
    private readonly ILogger<SeederFactory<TContext>> _logger;
    private readonly IReadOnlyList<SeederBase<TContext>> _seeders;

    public SeederFactory(ILogger<SeederFactory<TContext>> logger, IEnumerable<SeederBase<TContext>> registeredSeeders)
    {
        _logger = logger;
        _seeders = registeredSeeders.ToArray();
        _logger.LogInformation("{SeederCount} seeders loaded.", _seeders.Count);
    }

    public async Task SeedAsync(TContext context, CancellationToken cancellationToken = default)
    {
        foreach (
            var seeder in _seeders.OrderBy(seeder => seeder.Order).ThenBy(seeder => seeder.Name, StringComparer.Ordinal)
        )
        {
            _logger.LogInformation("Executing seeder: {SeederName} (Order: {Order})", seeder.Name, seeder.Order);
            await seeder.SeedAsync(context, cancellationToken);
        }

        _logger.LogInformation("All {Count} seeders completed successfully.", _seeders.Count);
    }
}
