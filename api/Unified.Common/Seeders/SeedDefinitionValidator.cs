namespace Unified.Common.Seeding;

/// <summary>
/// Validates that configured seed definitions do not duplicate their identifying values.
/// </summary>
public static class SeedDefinitionValidator
{
    public static void ThrowIfDuplicateValues<TDefinition>(
        IEnumerable<(TDefinition Definition, string Source)> definitions,
        string entityName,
        params (
            Func<TDefinition, string> KeySelector,
            string KeyName,
            IEqualityComparer<string> Comparer
        )[] keys
    )
    {
        var definitionArray = definitions.ToArray();
        var errors = keys
            .SelectMany(key =>
                DuplicateErrors(definitionArray, key.KeySelector, key.KeyName, key.Comparer)
            )
            .ToArray();

        if (errors.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate {entityName} seed values detected: {string.Join(", ", errors)}"
            );
        }
    }

    private static IEnumerable<string> DuplicateErrors<TDefinition>(
        IEnumerable<(TDefinition Definition, string Source)> definitions,
        Func<TDefinition, string> keySelector,
        string keyName,
        IEqualityComparer<string> comparer
    ) =>
        definitions
            .GroupBy(item => keySelector(item.Definition), comparer)
            .Where(group => group.Count() > 1)
            .Select(group =>
                $"{keyName} '{group.Key}' from {string.Join(", ", group.Select(item => item.Source).Distinct())}"
            );
}