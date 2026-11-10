using System.ComponentModel.DataAnnotations;

namespace Unified.Calendar.Options;

public sealed class CalendarConflictRulesOptions : IValidatableObject
{
    public const string SectionName = "CalendarConflicts";

    private static readonly HashSet<string> KnownTypes = new(StringComparer.Ordinal)
    {
        "shift",
        "assignment",
        "leave",
        "holiday",
        "training",
    };

    public Dictionary<string, string[]>? Rules { get; set; }

    public bool ShouldReport(string sourceType, string targetType) =>
        Rules is not null
        && Rules.TryGetValue(sourceType, out var targets)
        && targets is not null
        && targets.Contains(targetType, StringComparer.Ordinal);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Rules is not { Count: > 0 })
        {
            yield return new ValidationResult("CalendarConflicts:Rules must be configured.");
            yield break;
        }

        foreach (var (source, targets) in Rules)
        {
            if (!KnownTypes.Contains(source))
                yield return new ValidationResult($"CalendarConflicts:Rules contains unknown event type '{source}'.");

            if (targets is null)
            {
                yield return new ValidationResult(
                    $"CalendarConflicts:Rules:{source} must be an array (which may be empty)."
                );
                continue;
            }

            foreach (var target in targets)
                if (!KnownTypes.Contains(target))
                    yield return new ValidationResult(
                        $"CalendarConflicts:Rules:{source} contains unknown event type '{target}'."
                    );
        }
    }
}
