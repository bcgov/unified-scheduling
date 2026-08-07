using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Unified.Api.Models;
using Unified.Api.Options;
using Unified.Calendar.FeatureFlags;
using Unified.Common.FeatureFlags;
using Unified.Stats.FeatureFlags;
using Unified.Training.FeatureFlags;
using Unified.UserManagement.FeatureFlags;

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
        var featureFlagsPayload = new FeatureFlagsResponse
        {
            UserManagement = featureFlags.OfType<UserManagementFeatureFlags>().FirstOrDefault(),
            Calendar = featureFlags.OfType<CalendarFeatureFlags>().FirstOrDefault(),
            Stats = featureFlags.OfType<StatsFeatureFlags>().FirstOrDefault(),
            Training = featureFlags.OfType<TrainingFeatureFlags>().FirstOrDefault(),
        };

        var response = new ConfigResponse
        {
            FeatureFlags = featureFlagsPayload,
            SupportEmail = applicationOptions.Value.SupportEmail,
            ApplicationName = applicationOptions.Value.Name,
        };

        logger.LogDebug(
            "Configuration retrieved with module flags - UserManagement: {HasUserManagement}, Calendar: {HasCalendar}, Stats: {HasStats}, Training: {HasTraining}",
            featureFlagsPayload.UserManagement is not null,
            featureFlagsPayload.Calendar is not null,
            featureFlagsPayload.Stats is not null,
            featureFlagsPayload.Training is not null
        );
        return Ok(response);
    }
}
