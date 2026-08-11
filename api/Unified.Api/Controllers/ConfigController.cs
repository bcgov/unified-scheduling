using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Unified.Api.Models;
using Unified.Api.Options;
using Unified.Common.FeatureFlags;

namespace Unified.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController(
    ILogger<ConfigController> logger,
    IEnumerable<IFeatureFlags> featureFlags,
    IOptions<ApplicationOptions> applicationOptions
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
        var featureFlagsResponse = featureFlags.ToDictionary(f => f.Source, f => (object)f);

        var response = new ConfigResponse
        {
            FeatureFlags = featureFlagsResponse,
            SupportEmail = applicationOptions.Value.SupportEmail,
            ApplicationName = applicationOptions.Value.Name,
        };

        logger.LogDebug(
            "Configuration retrieved with module flags for sources: {FeatureFlagSources}",
            string.Join(", ", featureFlagsResponse.Keys.OrderBy(source => source, StringComparer.OrdinalIgnoreCase))
        );
        return Ok(response);
    }
}
