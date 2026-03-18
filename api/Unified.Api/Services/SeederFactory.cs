using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;

namespace Unified.Api.Services;

public class SeederFactory<TContext>
    where TContext : DbContext
{
    private readonly ILogger<SeederFactory<TContext>> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<SeederBase<TContext>> _seeders = [];

    public SeederFactory(ILogger<SeederFactory<TContext>> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        LoadSeeders();
    }

    public void LoadSeeders()
    {
        _seeders.Clear();
        _logger.LogInformation("Loading seeders...");

        var seederBaseType = typeof(SeederBase<TContext>);
        var seederTypes = AppDomain
            .CurrentDomain.GetAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(type => type is { IsAbstract: false, IsInterface: false, ContainsGenericParameters: false })
            .Where(type => seederBaseType.IsAssignableFrom(type) && type != seederBaseType)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

        foreach (var seederType in seederTypes)
        {
            var seeder = (SeederBase<TContext>)ActivatorUtilities.CreateInstance(_serviceProvider, seederType);
            _seeders.Add(seeder);
        }

        _logger.LogInformation("{Count} seeders loaded.", _seeders.Count);
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

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null).Cast<Type>();
        }
    }
}
