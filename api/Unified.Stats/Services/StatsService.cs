using System;
using System.Collections.Generic;
using System.Text;

namespace Unified.Stats.Services;

internal class StatsService : IStatsService
{
    public string ModuleName => "Stats";

    public string CheckHealth()
    {
        return $"{ModuleName} Loaded Successfully";
    }
}
