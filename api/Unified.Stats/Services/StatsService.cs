using System;
using System.Collections.Generic;
using System.Text;

namespace Unified.Stats.Services;

internal class StatsService : IStatsService
{
    public string ModuleName => "Stats Module Loaded Successfully";

    public string CheckHealth()
    {
        return ModuleName;
    }
}
