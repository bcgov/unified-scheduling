using System.Net.Http.Headers;
using System.Text;
using JCCommon.Clients.LocationServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.JCInterface.Options;
using Unified.JCInterface.Services;

namespace Unified.JCInterface;

/// <summary>
/// Registers all JC Interface services.
/// Call <c>builder.Services.AddJCInterfaceModule()</c> from Program.cs.
/// </summary>
public static class JCInterfaceModule
{
    public static IServiceCollection AddJCInterfaceModule(this IServiceCollection services)
    {
        // Bind and validate the combined options (auth + behaviour)
        services
            .AddOptions<JCInterfaceOptions>()
            .BindConfiguration(JCInterfaceOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register the typed HttpClient for the JC Interface API.
        // Basic Auth credentials and the base URL come from JCInterfaceOptions
        // so no raw IConfiguration reads are needed in service constructors.
        services.AddHttpClient<LocationServicesClient>(
            (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<JCInterfaceOptions>>().Value;

                client.BaseAddress = new Uri(options.Url.EndsWith('/') ? options.Url : options.Url + "/");
                client.Timeout = options.HttpTimeout;

                var credentials = Convert.ToBase64String(
                    Encoding.ASCII.GetBytes($"{options.Username}:{options.Password}")
                );
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }
        );

        // Register the sync orchestrator as scoped — it holds a DbContext.
        services.AddScoped<JCDataUpdaterService>();

        return services;
    }
}
