using System;
using System.Collections.Generic;
using System.Text;

namespace Unified.Stats.Services;

internal interface IStatsService
{
    string ModuleName { get; }

    string CheckHealth();
}
