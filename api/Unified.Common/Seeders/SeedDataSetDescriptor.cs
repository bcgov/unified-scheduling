using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Unified.Common.Seeding;

public sealed record SeedDataSetDescriptor(
    string Key,
    IReadOnlyList<ISeedConfiguration> Configurations,
    string? RequiredFeature = null,
    Func<IConfiguration, bool>? AvailableWhen = null
)
{
    public bool IsAvailable(IConfiguration configuration) => AvailableWhen?.Invoke(configuration) ?? true;

    public void Register(IServiceCollection services)
    {
        foreach (var configuration in Configurations)
        {
            services.AddSingleton(configuration.GetType(), configuration);
        }
    }
}
