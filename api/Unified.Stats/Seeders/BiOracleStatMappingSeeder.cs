using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Stats;

namespace Unified.Stats.Seeders;

public sealed class BiOracleStatMappingSeeder(
    ILogger<BiOracleStatMappingSeeder> logger,
    IEnumerable<BiOracleStatMappingSeedConfiguration> configurations,
    TimeProvider? timeProvider = null
) : SeederBase<UnifiedDbContext>(logger)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public override int Order => 16;

    public override string Name => "BiOracleStatMapping";

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        var configuredMappingSets = configurations.SelectMany(configuration => configuration.Definitions).ToArray();

        if (configuredMappingSets.Length == 0)
        {
            Logger.LogWarning(
                "No BiOracle mapping seed data is configured; an approved effective date and exact SubCategoryMetric crosswalk are required."
            );
            return;
        }

        var mappingSets = await ResolveEffectiveDatesAsync(dbContext, configuredMappingSets, cancellationToken);
        var archivedMetricIds = await ValidateSubCategoryMetricsAsync(dbContext, mappingSets, cancellationToken);
        var plans = new List<(
            BiOracleStatMappingSetSeedDefinition Definition,
            BiOracleStatMappingSet? ExistingSet,
            BiOracleStatMappingSeedDefinition[] MissingMappings
        )>();

        foreach (var definition in mappingSets)
        {
            var mappingSet = await dbContext.BiOracleStatMappingSets.FirstOrDefaultAsync(
                entity => entity.Id == definition.Id,
                cancellationToken
            );

            var missingMappings = await ValidateMappingsAsync(
                dbContext,
                definition,
                mappingSet is null,
                archivedMetricIds,
                cancellationToken
            );
            plans.Add((definition, mappingSet, missingMappings));
        }

        // Validate every set before staging writes, including expiry changes for approved versions.
        foreach (var (definition, mappingSet, missingMappings) in plans)
        {
            if (mappingSet is null)
            {
                await dbContext.BiOracleStatMappingSets.AddAsync(
                    new BiOracleStatMappingSet
                    {
                        Id = definition.Id,
                        EffectiveDate = definition.EffectiveDate!.Value,
                        ExpiryDate = definition.ExpiryDate,
                    },
                    cancellationToken
                );
            }
            else
            {
                mappingSet.ExpiryDate = definition.ExpiryDate;
            }

            foreach (var mapping in missingMappings)
            {
                await dbContext.BiOracleStatMappings.AddAsync(
                    new BiOracleStatMapping
                    {
                        Id = mapping.Id,
                        MappingSetId = definition.Id,
                        SubCategoryMetricId = mapping.SubCategoryMetricId,
                        TargetTable = mapping.TargetTable,
                        TargetColumn = mapping.TargetColumn,
                    },
                    cancellationToken
                );
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<BiOracleStatMappingSetSeedDefinition[]> ResolveEffectiveDatesAsync(
        UnifiedDbContext dbContext,
        IReadOnlyCollection<BiOracleStatMappingSetSeedDefinition> mappingSets,
        CancellationToken cancellationToken
    )
    {
        var existingSets = await dbContext.BiOracleStatMappingSets.ToDictionaryAsync(
            mappingSet => mappingSet.Id,
            cancellationToken
        );
        var seedDate = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        var resolvedSets = mappingSets
            .Select(mappingSet =>
            {
                existingSets.TryGetValue(mappingSet.Id, out var existing);
                return mappingSet with
                {
                    EffectiveDate = existing?.EffectiveDate ?? mappingSet.EffectiveDate ?? seedDate,
                    ExpiryDate = mappingSet.ExpiryDate ?? existing?.ExpiryDate,
                };
            })
            .ToArray();

        BiOracleStatMappingSeedValidator.Validate(
            resolvedSets
                .Concat(
                    existingSets.Values
                        .Where(existing => !resolvedSets.Any(definition => definition.Id == existing.Id))
                        .Select(existing => new BiOracleStatMappingSetSeedDefinition
                        {
                            Id = existing.Id,
                            EffectiveDate = existing.EffectiveDate,
                            ExpiryDate = existing.ExpiryDate,
                            Mappings = [],
                        })
                )
                .ToArray()
        );
        return resolvedSets;
    }

    private static async Task<HashSet<int>> ValidateSubCategoryMetricsAsync(
        UnifiedDbContext dbContext,
        IEnumerable<BiOracleStatMappingSetSeedDefinition> mappingSets,
        CancellationToken cancellationToken
    )
    {
        var requestedIds = mappingSets
            .SelectMany(mappingSet => mappingSet.Mappings)
            .Select(mapping => mapping.SubCategoryMetricId)
            .Distinct()
            .ToArray();
        var metrics = await dbContext
            .SubCategoryMetrics.Where(metric => requestedIds.Contains(metric.Id))
            .Select(metric => new { metric.Id, metric.IsArchived })
            .ToArrayAsync(cancellationToken);

        var missingIds = requestedIds.Except(metrics.Select(metric => metric.Id)).ToArray();
        if (missingIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"BiOracle mappings reference missing SubCategoryMetric IDs: {string.Join(", ", missingIds)}."
            );
        }

        return metrics.Where(metric => metric.IsArchived).Select(metric => metric.Id).ToHashSet();
    }

    private static async Task<BiOracleStatMappingSeedDefinition[]> ValidateMappingsAsync(
        UnifiedDbContext dbContext,
        BiOracleStatMappingSetSeedDefinition mappingSet,
        bool isNewMappingSet,
        HashSet<int> archivedMetricIds,
        CancellationToken cancellationToken
    )
    {
        var requestedIds = mappingSet.Mappings.Select(mapping => mapping.Id).ToArray();
        var existingMappings = await dbContext
            .BiOracleStatMappings.Where(mapping =>
                mapping.MappingSetId == mappingSet.Id || requestedIds.Contains(mapping.Id)
            )
            .ToArrayAsync(cancellationToken);
        var conflictingMapping = existingMappings.FirstOrDefault(mapping => mapping.MappingSetId != mappingSet.Id);
        if (conflictingMapping is not null)
        {
            throw new InvalidOperationException(
                $"BiOracle mapping {conflictingMapping.Id} already belongs to mapping set {conflictingMapping.MappingSetId} and cannot be repurposed for set {mappingSet.Id}."
            );
        }

        var unexpectedIds = existingMappings
            .Select(mapping => mapping.Id)
            .Except(requestedIds)
            .ToArray();
        if (unexpectedIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"BiOracle mapping set {mappingSet.Id} contains historical mappings absent from seed data: {string.Join(", ", unexpectedIds)}."
            );
        }

        var missingMappings = new List<BiOracleStatMappingSeedDefinition>();
        foreach (var definition in mappingSet.Mappings)
        {
            var existing = existingMappings.SingleOrDefault(mapping => mapping.Id == definition.Id);
            if (existing is null)
            {
                if (archivedMetricIds.Contains(definition.SubCategoryMetricId))
                {
                    throw new InvalidOperationException(
                        $"BiOracle mappings reference archived SubCategoryMetric IDs: {definition.SubCategoryMetricId}."
                    );
                }

                missingMappings.Add(definition);
                continue;
            }

            if (
                existing.SubCategoryMetricId != definition.SubCategoryMetricId
                || existing.TargetTable != definition.TargetTable
                || existing.TargetColumn != definition.TargetColumn
            )
            {
                throw new InvalidOperationException(
                    $"BiOracle mapping {definition.Id} is immutable and differs from its seed definition."
                );
            }
        }

        if (
            !isNewMappingSet
            && missingMappings.Count > 0
            && await dbContext.BiOracleEtlRuns.AnyAsync(run => run.MappingSetId == mappingSet.Id, cancellationToken)
        )
        {
            throw new InvalidOperationException(
                $"BiOracle mapping set {mappingSet.Id} has ETL history and cannot accept new mappings. An approved new effective-dated set with globally unique mapping IDs and a non-overlapping expiry date for the historical set is required."
            );
        }

        return missingMappings.ToArray();
    }
}
