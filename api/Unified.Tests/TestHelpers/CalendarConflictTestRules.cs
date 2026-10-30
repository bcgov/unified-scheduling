using Microsoft.Extensions.Options;
using Unified.Calendar.Options;

namespace Unified.Tests.TestHelpers;

internal static class CalendarConflictTestRules
{
    // Existing scheduling tests explicitly exercise assignment self-conflicts and supplemental shift/leave pairs.
    public static IOptions<CalendarConflictRulesOptions> Scheduling =>
        Create(("assignment", ["assignment", "holiday", "training", "leave"]), ("shift", ["leave"]));

    public static IOptions<CalendarConflictRulesOptions> Create(params (string Source, string[] Targets)[] rules) =>
        Options.Create(
            new CalendarConflictRulesOptions
            {
                Rules = rules.ToDictionary(rule => rule.Source, rule => rule.Targets, StringComparer.Ordinal),
            }
        );
}
