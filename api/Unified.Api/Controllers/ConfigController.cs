using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Unified.Api.Models;
using Unified.Api.Options;
using Unified.Common.FeatureFlags;
using Unified.Infrastructure.Email.Ches;
using Unified.Scheduling.Options;

namespace Unified.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController(
    ILogger<ConfigController> logger,
    IEnumerable<IFeatureFlags> featureFlags,
    IOptions<ApplicationOptions> applicationOptions,
    IOptions<BrandingOptions> brandingOptions,
    IConfiguration configuration,
    IOptions<WorkingHoursOptions> workingHoursOptions
) : ControllerBase
{
    /// <summary>
    /// Get application configuration including feature flags organized by module.
    /// </summary>
    /// <returns>Configuration response with module-organized feature flags</returns>
    [HttpGet]
    [AllowAnonymous]
    public ActionResult<ConfigResponse> Get()
    {
        logger.LogDebug("Retrieving application configuration");
        var featureFlagsResponse = featureFlags
            .DistinctBy(f => f.Source, StringComparer.Ordinal)
            .ToDictionary(f => f.Source, f => (object)f, StringComparer.Ordinal);

        var response = new ConfigResponse
        {
            FeatureFlags = featureFlagsResponse,
            SupportEmail = applicationOptions.Value.SupportEmail,
            ApplicationName = applicationOptions.Value.Name,
            SelectedBrand = brandingOptions.Value.SelectedBrand,
            BrandingOptions = brandingOptions.Value.Brands,
            EmailDistributionAvailable = ChesEmailConfiguration.IsEnabled(configuration),
            DefaultLunchMinutes = workingHoursOptions.Value.DefaultLunchMinutes,
        };

        logger.LogDebug(
            "Configuration retrieved with module flags for sources: {FeatureFlagSources}",
            string.Join(", ", featureFlagsResponse.Keys.OrderBy(source => source, StringComparer.OrdinalIgnoreCase))
        );
        return Ok(response);
    }
}
