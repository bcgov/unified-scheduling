using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unified.Api.Models;
using Unified.Flags;

namespace Unified.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly ILogger<ConfigController> _logger;
    private readonly FeatureFlagsOptions _featureFlagsOptions;

    public ConfigController(ILogger<ConfigController> logger, FeatureFlagsOptions featureFlagsOptions)
    {
        _logger = logger;
        _featureFlagsOptions = featureFlagsOptions;
    }

    [HttpGet]
    [AllowAnonymous]
    public ActionResult<ConfigResponse> Get()
    {
        var response = new ConfigResponse { FeatureFlags = _featureFlagsOptions };
        return Ok(response);
    }
}
