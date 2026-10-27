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
        BiOracleStatMappingSeedValidator.Validate(mappingSets);
        var archivedMetricIds = await ValidateSubCategoryMetricsAsync(dbContext, mappingSets, cancellationToken);

        foreach (var definition in mappingSets)
        {
            var mappingSet = await dbContext.BiOracleStatMappingSets.FirstOrDefaultAsync(
                entity => entity.Id == definition.Id,
                cancellationToken
            );

            var isNewMappingSet = mappingSet is null;
            if (mappingSet is null)
            {
                mappingSet = new BiOracleStatMappingSet
                {
                    Id = definition.Id,
                    EffectiveDate = definition.EffectiveDate!.Value,
                    ExpiryDate = definition.ExpiryDate,
                };
                await dbContext.BiOracleStatMappingSets.AddAsync(mappingSet, cancellationToken);
            }
            else
            {
                if (mappingSet.EffectiveDate != definition.EffectiveDate!.Value)
                {
                    throw new InvalidOperationException(
                        $"BiOracle mapping set {definition.Id} has already been assigned a different effective date."
                    );
                }

                mappingSet.ExpiryDate = definition.ExpiryDate;
            }

            await SeedMappingsAsync(dbContext, definition, isNewMappingSet, archivedMetricIds, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await StatsSeedIdentitySequence.AdvanceAsync(dbContext, "etl", "BiOracleStatMappings", cancellationToken);
    }

    private async Task<BiOracleStatMappingSetSeedDefinition[]> ResolveEffectiveDatesAsync(
        UnifiedDbContext dbContext,
        IReadOnlyCollection<BiOracleStatMappingSetSeedDefinition> mappingSets,
        CancellationToken cancellationToken
    )
    {
        var runtimeDatedIds = mappingSets
            .Where(mappingSet => mappingSet.EffectiveDate is null)
            .Select(mappingSet => mappingSet.Id)
            .ToArray();
        var existingEffectiveDates = await dbContext
            .BiOracleStatMappingSets.Where(mappingSet => runtimeDatedIds.Contains(mappingSet.Id))
            .ToDictionaryAsync(mappingSet => mappingSet.Id, mappingSet => mappingSet.EffectiveDate, cancellationToken);
        var seedDate = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        return mappingSets
            .Select(mappingSet =>
                mappingSet.EffectiveDate is not null
                    ? mappingSet
                    : mappingSet with
                    {
                        // Fresh installs after a cutover still need a valid historical set.
                        // A persisted set always retains its original effective date.
                        EffectiveDate = existingEffectiveDates.GetValueOrDefault(
                            mappingSet.Id,
                            mappingSet.ExpiryDate is { } expiryDate && seedDate > expiryDate ? expiryDate : seedDate
                        ),
                    }
            )
            .ToArray();
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

    private static async Task SeedMappingsAsync(
        UnifiedDbContext dbContext,
        BiOracleStatMappingSetSeedDefinition mappingSet,
        bool isNewMappingSet,
        HashSet<int> archivedMetricIds,
        CancellationToken cancellationToken
    )
    {
        var existingMappings = isNewMappingSet
            ? Array.Empty<BiOracleStatMapping>()
            : await dbContext
                .BiOracleStatMappings.Where(mapping => mapping.MappingSetId == mappingSet.Id)
                .ToArrayAsync(cancellationToken);

        var unexpectedIds = existingMappings
            .Select(mapping => mapping.Id)
            .Except(mappingSet.Mappings.Select(mapping => mapping.Id))
            .ToArray();
        if (unexpectedIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"BiOracle mapping set {mappingSet.Id} contains historical mappings absent from seed data: {string.Join(", ", unexpectedIds)}."
            );
        }

        foreach (var definition in mappingSet.Mappings)
        {
            var existing = existingMappings.SingleOrDefault(mapping => mapping.Id == definition.Id);
            if (existing is null)
            {
                if (!isNewMappingSet)
                {
                    throw new InvalidOperationException(
                        $"BiOracle mapping set {mappingSet.Id} is persisted and cannot accept new mappings."
                    );
                }

                if (archivedMetricIds.Contains(definition.SubCategoryMetricId))
                {
                    throw new InvalidOperationException(
                        $"BiOracle mappings reference archived SubCategoryMetric IDs: {definition.SubCategoryMetricId}."
                    );
                }

                await dbContext.BiOracleStatMappings.AddAsync(
                    new BiOracleStatMapping
                    {
                        Id = definition.Id,
                        MappingSetId = mappingSet.Id,
                        SubCategoryMetricId = definition.SubCategoryMetricId,
                        TargetTable = definition.TargetTable,
                        TargetColumn = definition.TargetColumn,
                    },
                    cancellationToken
                );
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
    }
}
