using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.Authorization;
using Unified.Common.FeatureFlags;
using Unified.Common.Options;
using Unified.Reporting.FeatureFlags;
using Unified.Reporting.Services.Reporting;

namespace Unified.Reporting;

public static class ReportingModule
{
    public static bool IsModuleEnabled(IConfiguration config)
    {
        var enabled = config.GetSection(ReportingFeatureFlags.Section).Get<ReportingFeatureFlags>()?.Enabled ?? false;
        return enabled;
    }

    public static bool IsModuleEnabled(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<ReportingFeatureFlags>>();
        return options.Value.Enabled;
    }

    public static IServiceCollection AddReportingModule(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<ReportingFeatureFlags>()
            .BindConfiguration(ReportingFeatureFlags.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<ReportingFeatureFlags>,
            RequiredBooleanOptionsValidator<ReportingFeatureFlags>
        >();
        services.AddSingleton<IFeatureFlags>(sp => sp.GetRequiredService<IOptions<ReportingFeatureFlags>>().Value);

        if (!IsModuleEnabled(config))
        {
            return services;
        }

        services.AddAuthorizationBuilder().AddPermissionPolicy(Permissions.ReportsGenerate);

        services.AddScoped<IReportQueryService, ReportQueryService>();
        return services;
    }
}
