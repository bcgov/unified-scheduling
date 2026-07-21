using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Unified.Api.Models;
using Unified.Api.Options;
using Unified.FeatureFlags;

namespace Unified.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly ILogger<ConfigController> _logger;
    private readonly IFeatureFlags _featureFlags;
    private readonly ApplicationOptions _applicationOptions;

    public ConfigController(
        ILogger<ConfigController> logger,
        IFeatureFlags featureFlags,
        IOptions<ApplicationOptions> applicationOptions
    )
    {
        _logger = logger;
        _featureFlags = featureFlags;
        _applicationOptions = applicationOptions.Value;
    }

    [HttpGet]
    [AllowAnonymous]
    public ActionResult<ConfigResponse> Get()
    {
        var response = new ConfigResponse
        {
            FeatureFlags = _featureFlags.Current,
            SupportEmail = _applicationOptions.SupportEmail,
            ApplicationName = _applicationOptions.Name,
        };
        return Ok(response);
    }
}
