using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Stats;

namespace Unified.Stats.Seeders;

public sealed class BiOracleLocationMappingSeeder(
    ILogger<BiOracleLocationMappingSeeder> logger,
    IEnumerable<BiOracleLocationMappingSeedConfiguration> configurations
) : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 15;

    public override string Name => "BiOracleLocationMapping";

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        var definitions = configurations
            .SelectMany(configuration =>
                configuration.Definitions.Select(definition => (Definition: definition, configuration.Source))
            )
            .ToArray();
        ValidateDefinitions(definitions);

        foreach (var (definition, _) in definitions)
        {
            var mapping = await dbContext.BiOracleLocationMappings.FirstOrDefaultAsync(
                entity => entity.Id == definition.Id,
                cancellationToken
            );

            if (mapping is null)
            {
                await dbContext.BiOracleLocationMappings.AddAsync(
                    new BiOracleLocationMapping
                    {
                        Id = definition.Id,
                        JustinLocationCode = definition.JustinLocationCode,
                        CrtLocId = definition.CrtLocId,
                        EffectiveDate = definition.EffectiveDate,
                        ExpiryDate = definition.ExpiryDate,
                    },
                    cancellationToken
                );
                continue;
            }

            mapping.JustinLocationCode = definition.JustinLocationCode;
            mapping.CrtLocId = definition.CrtLocId;
            mapping.EffectiveDate = definition.EffectiveDate;
            mapping.ExpiryDate = definition.ExpiryDate;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateDefinitions(
        IReadOnlyCollection<(BiOracleLocationMappingSeedDefinition Definition, string Source)> definitions
    )
    {
        var invalidCode = definitions.FirstOrDefault(item =>
            string.IsNullOrWhiteSpace(item.Definition.JustinLocationCode)
            || item.Definition.JustinLocationCode.Length != 4
        );
        if (invalidCode.Definition is not null)
        {
            throw new InvalidOperationException(
                $"BiOracle location mapping {invalidCode.Definition.Id} requires a four-character JUSTIN location code."
            );
        }

        var invalidEffectivePeriod = definitions.FirstOrDefault(item =>
            item.Definition.ExpiryDate < item.Definition.EffectiveDate
        );
        if (invalidEffectivePeriod.Definition is not null)
        {
            throw new InvalidOperationException(
                $"BiOracle location mapping {invalidEffectivePeriod.Definition.Id} expires before its effective date."
            );
        }

        SeedDefinitionValidator.ThrowIfDuplicateValues(
            definitions,
            "BiOracle location mapping",
            (mapping => mapping.Id.ToString(), "Id", StringComparer.Ordinal),
            (mapping => mapping.JustinLocationCode, "JustinLocationCode", StringComparer.Ordinal),
            (mapping => mapping.CrtLocId.ToString(), "CrtLocId", StringComparer.Ordinal)
        );
    }
}
