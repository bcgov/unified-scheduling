using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Unified.Core.Pdf;

namespace Unified.Infrastructure.Pdf;

public static class PlaywrightPdfServiceCollectionExtensions
{
    public static IServiceCollection AddPlaywrightPdfRendering(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<ScheduleDistributionRenderingOptions>()
            .Bind(configuration.GetSection(ScheduleDistributionRenderingOptions.SectionName));
        services.AddSingleton<
            IValidateOptions<ScheduleDistributionRenderingOptions>,
            ScheduleDistributionRenderingOptionsValidator
        >();
        services.AddSingleton<ISchedulePdfRenderer, PlaywrightSchedulePdfRenderer>();

        return services;
    }
}
