using Unified.Db.Models.Stats;

namespace Unified.Stats.Seeders;

public static class BiOracleStatMappingSeedValidator
{
    private static readonly IReadOnlySet<string> ProhibitedTargetColumns = new HashSet<string>(StringComparer.Ordinal)
    {
        "CRT_LOC_ID",
        "MONTH",
        "SUPERVISOR_YN",
        "CREATE_DATE",
        "CREATED_BY",
        "UPDATE_DATE",
        "UPDATED_BY",
    };

    public static void Validate(IReadOnlyCollection<BiOracleStatMappingSetSeedDefinition> mappingSets)
    {
        if (mappingSets.Count == 0)
        {
            return;
        }

        var unresolvedSet = mappingSets.FirstOrDefault(definition => definition.EffectiveDate is null);
        if (unresolvedSet is not null)
        {
            throw new InvalidOperationException(
                $"BiOracle mapping set {unresolvedSet.Id} has an unresolved effective date."
            );
        }

        ThrowIfDuplicate(mappingSets, definition => definition.Id, "mapping-set Id");
        ThrowIfDuplicate(mappingSets, definition => definition.EffectiveDate, "mapping-set effective date");

        if (mappingSets.Count(definition => definition.ExpiryDate is null) != 1)
        {
            throw new InvalidOperationException(
                "BiOracle mapping seed data must contain exactly one unexpired mapping set."
            );
        }

        ThrowIfDuplicate(
            mappingSets.SelectMany(mappingSet => mappingSet.Mappings),
            definition => definition.Id,
            "mapping Id"
        );

        var orderedSets = mappingSets.OrderBy(definition => definition.EffectiveDate!.Value).ToArray();
        for (var index = 0; index < orderedSets.Length; index++)
        {
            var mappingSet = orderedSets[index];
            if (mappingSet.ExpiryDate < mappingSet.EffectiveDate!.Value)
            {
                throw new InvalidOperationException(
                    $"BiOracle mapping set {mappingSet.Id} expires before its effective date."
                );
            }

            if (
                index > 0
                && (
                    orderedSets[index - 1].ExpiryDate is null
                    || mappingSet.EffectiveDate.Value <= orderedSets[index - 1].ExpiryDate
                )
            )
            {
                throw new InvalidOperationException(
                    $"BiOracle mapping sets {orderedSets[index - 1].Id} and {mappingSet.Id} have overlapping effective periods."
                );
            }

            ValidateMappings(mappingSet);
        }
    }

    private static void ValidateMappings(BiOracleStatMappingSetSeedDefinition mappingSet)
    {
        ThrowIfDuplicate(
            mappingSet.Mappings,
            definition => new
            {
                definition.SubCategoryMetricId,
                definition.TargetTable,
                definition.TargetColumn,
            },
            "mapping target tuple"
        );

        foreach (var mapping in mappingSet.Mappings)
        {
            if (!BiOracleTargetTable.Values.Contains(mapping.TargetTable))
            {
                throw new InvalidOperationException(
                    $"BiOracle mapping {mapping.Id} uses unsupported target table '{mapping.TargetTable}'."
                );
            }

            if (string.IsNullOrWhiteSpace(mapping.TargetColumn))
            {
                throw new InvalidOperationException($"BiOracle mapping {mapping.Id} requires a target column.");
            }

            if (
                mapping.TargetColumn.Length > 30
                || mapping.TargetColumn != mapping.TargetColumn.Trim().ToUpperInvariant()
            )
            {
                throw new InvalidOperationException(
                    $"BiOracle mapping {mapping.Id} target column must be an uppercase Oracle identifier no longer than 30 characters."
                );
            }

            if (ProhibitedTargetColumns.Contains(mapping.TargetColumn))
            {
                throw new InvalidOperationException(
                    $"BiOracle mapping {mapping.Id} targets control column '{mapping.TargetColumn}'."
                );
            }
        }
    }

    private static void ThrowIfDuplicate<TDefinition, TKey>(
        IEnumerable<TDefinition> definitions,
        Func<TDefinition, TKey> keySelector,
        string keyName
    )
        where TKey : notnull
    {
        var duplicate = definitions.GroupBy(keySelector).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Duplicate BiOracle {keyName} '{duplicate.Key}' detected.");
        }
    }
}
