using System.Net.Http.Headers;
using System.Text;
using JCCommon.Clients.LocationServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.JCInterface.Options;
using Unified.JCInterface.Services;

namespace Unified.JCInterface;

/// <summary>
/// Registers all JC Interface services.
/// Call <c>builder.Services.AddJCInterfaceModule(builder.Configuration)</c> from Program.cs.
/// </summary>
public static class JCInterfaceModule
{
    public static IServiceCollection AddJCInterfaceModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Read SkipSync directly from configuration (before the options pipeline exists)
        // so we can decide whether data-annotation validation should run at all. When
        // SkipSync is true, Url/Username/Password aren't required and shouldn't block startup.
        var skipSync = configuration.GetValue<bool>(
            $"{JCInterfaceOptions.SectionName}:{nameof(JCInterfaceOptions.SkipSync)}"
        );

        var optionsBuilder = services
            .AddOptions<JCInterfaceOptions>()
            .BindConfiguration(JCInterfaceOptions.SectionName);

        // Only validate (and enforce at startup) when the JC-Interface is actually going
        // to be used — the [Required]/[HttpsUrl] attributes on Url/Username/Password would
        // otherwise fail validation in environments that intentionally skip the sync.
        if (!skipSync)
        {
            optionsBuilder.ValidateDataAnnotations().ValidateOnStart();
        }

        // Register the typed HttpClient for the JC Interface API.
        // Basic Auth credentials and the base URL come from JCInterfaceOptions
        // so no raw IConfiguration reads are needed in service constructors.
        services.AddHttpClient<LocationServicesClient>(
            (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<JCInterfaceOptions>>().Value;

                if (string.IsNullOrWhiteSpace(options.Url))
                {
                    // SkipSync is enabled and no Url was configured; leave the client
                    // unconfigured since it should never be called in this mode.
                    return;
                }

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
