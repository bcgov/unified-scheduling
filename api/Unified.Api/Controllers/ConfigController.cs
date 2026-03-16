using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unified.Api.Models;
using Unified.FeatureFlags;

namespace Unified.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly ILogger<ConfigController> _logger;
    private readonly IFeatureFlags _featureFlags;

    public ConfigController(ILogger<ConfigController> logger, IFeatureFlags featureFlags)
    {
        _logger = logger;
        _featureFlags = featureFlags;
    }

    [HttpGet]
    [AllowAnonymous]
    public ActionResult<ConfigResponse> Get()
    {
        var response = new ConfigResponse { FeatureFlags = _featureFlags.Current };
        return Ok(response);
    }
}
