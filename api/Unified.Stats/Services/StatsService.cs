using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Unified.Stats.Services;

internal class StatsService(ILogger<StatsService> logger) : IStatsService
{
    public string ModuleName => "Stats";

    public string CheckHealth()
    {
        logger.LogDebug("Checking stats module health");

        return $"{ModuleName} Loaded Successfully";
    }
}
