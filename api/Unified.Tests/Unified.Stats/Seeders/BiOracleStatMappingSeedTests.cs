using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Db;
using Unified.Db.Models.Stats;
using Unified.Stats.Seeders;

namespace Unified.Tests.Stats.Seeders;

public sealed class BiOracleStatMappingSeedTests
{
    private static readonly DateTimeOffset SeedTime = new(2026, 9, 26, 23, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ProductionData_HasExpectedMappingsAndCorrectedOthersTaxonomy()
    {
        var mappingSet = Assert.Single(BiOracleStatMappingSeedData.Definitions);

        Assert.Null(mappingSet.EffectiveDate);
        Assert.Equal(516, mappingSet.Mappings.Count);
        Assert.Equal(Enumerable.Range(1, 516), mappingSet.Mappings.Select(mapping => mapping.Id));
        Assert.Equal(
            210,
            mappingSet.Mappings.Select(mapping => (mapping.TargetTable, mapping.TargetColumn)).Distinct().Count()
        );
        Assert.Contains(
            mappingSet.Mappings,
            mapping => mapping.SubCategoryMetricId == 88 && mapping.TargetColumn == "CVF_OTHR_HRS"
        );
        Assert.Contains(
            mappingSet.Mappings,
            mapping => mapping.SubCategoryMetricId == 104 && mapping.TargetColumn == "CRIM_OTHR_HRS"
        );
        Assert.DoesNotContain(
            mappingSet.Mappings,
            mapping => mapping.SubCategoryMetricId == 84 && mapping.TargetColumn == "CVF_OTHR_HRS"
        );
        Assert.DoesNotContain(
            mappingSet.Mappings,
            mapping => mapping.SubCategoryMetricId == 100 && mapping.TargetColumn == "CRIM_OTHR_HRS"
        );
    }

    [Fact]
    public void ProductionData_HasApprovedLocationLevelMappingsOnly()
    {
        var mappings = Assert.Single(BiOracleStatMappingSeedData.Definitions).Mappings;
        (int SubCategoryMetricId, string TargetColumn)[] expectedMappings =
        [
            (366, "CCRT_KMS"),
            (367, "AIR_ESC_TRIPS_L1"),
            (368, "AIR_ESC_TRIPS_L2"),
            (369, "AIR_ESC_TRIPS_L3"),
            (371, "GRD_ESC_TRIPS_L1"),
            (372, "GRD_ESC_TRIPS_L2"),
            (373, "GRD_ESC_TRIPS_L3"),
            (375, "GRD_ESC_KMS_L1"),
            (376, "GRD_ESC_KMS_L2"),
            (377, "GRD_ESC_KMS_L3"),
            (375, "GRD_KM_TRAVL"),
            (376, "GRD_KM_TRAVL"),
            (377, "GRD_KM_TRAVL"),
            (398, "CRN_JRS_SMND"),
            (399, "CRN_PNLS"),
            (400, "JURORS_SUMD"),
            (401, "JURORS_PAID"),
            (402, "JURORS_PANELS_CNT"),
            (403, "JURORS_PAID_SUM"),
            (407, "HLD_CELL_HRS"),
            (433, "CVF_CRTORD_RCVD"),
            (434, "CVF_CRTORD_CONC"),
            (439, "CVF_WARR_RCVD"),
            (440, "CVF_WARR_CONC"),
            (441, "CRIM_CRTORD_RCVD"),
            (442, "CRIM_CRTORD_CONC"),
            (447, "CRIM_WARR_RCVD"),
            (448, "CRIM_WARR_CONC"),
        ];
        HashSet<int> locationLevelIds =
        [
            366,
            367,
            368,
            369,
            371,
            372,
            373,
            375,
            376,
            377,
            398,
            399,
            400,
            401,
            402,
            403,
            407,
            408,
            409,
            410,
            411,
            412,
            413,
            418,
            419,
            420,
            421,
            422,
            423,
            424,
            425,
            426,
            427,
            433,
            434,
            435,
            436,
            437,
            438,
            439,
            440,
            441,
            442,
            443,
            444,
            445,
            446,
            447,
            448,
        ];

        foreach (var expected in expectedMappings)
        {
            Assert.Contains(
                mappings,
                mapping =>
                    mapping.SubCategoryMetricId == expected.SubCategoryMetricId
                    && mapping.TargetTable == BiOracleTargetTable.SheriffServices
                    && mapping.TargetColumn == expected.TargetColumn
            );
        }

        Assert.DoesNotContain(
            mappings,
            mapping =>
                locationLevelIds.Contains(mapping.SubCategoryMetricId)
                && mapping.TargetTable == BiOracleTargetTable.SheriffServicesHours
        );
        Assert.DoesNotContain(mappings, mapping => new[] { 435, 436, 443, 444 }.Contains(mapping.SubCategoryMetricId));
        Assert.Equal(
            mappings.Count,
            mappings
                .Select(mapping => (mapping.SubCategoryMetricId, mapping.TargetTable, mapping.TargetColumn))
                .Distinct()
                .Count()
        );
    }

    [Fact]
    public void Validate_ManyToOneAndOneToManyMappings_DoesNotThrow()
    {
        var mappingSet = MappingSet(
            Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1"),
            Mapping(2, 101, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1"),
            Mapping(3, 100, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_REG_HRS_L1")
        );

        BiOracleStatMappingSeedValidator.Validate([mappingSet]);
    }

    [Fact]
    public void Validate_ExactDuplicateMapping_Throws()
    {
        var mappingSet = MappingSet(
            Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1"),
            Mapping(2, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1")
        );

        Assert.Throws<InvalidOperationException>(() => BiOracleStatMappingSeedValidator.Validate([mappingSet]));
    }

    [Fact]
    public void Validate_OverlappingMappingSets_Throws()
    {
        var first = MappingSet([], id: 1, effectiveDate: new DateOnly(2026, 1, 1), expiryDate: null);
        var second = MappingSet([], id: 2, effectiveDate: new DateOnly(2026, 9, 1), expiryDate: null);

        Assert.Throws<InvalidOperationException>(() => BiOracleStatMappingSeedValidator.Validate([first, second]));
    }

    [Fact]
    public void Validate_NoCurrentMappingSet_Throws()
    {
        var expired = MappingSet(
            [],
            id: 1,
            effectiveDate: new DateOnly(2026, 1, 1),
            expiryDate: new DateOnly(2026, 8, 31)
        );

        Assert.Throws<InvalidOperationException>(() => BiOracleStatMappingSeedValidator.Validate([expired]));
    }

    [Theory]
    [InlineData("UNKNOWN", "GRD_ESC_HRS_L1")]
    [InlineData(BiOracleTargetTable.SheriffServices, "")]
    [InlineData(BiOracleTargetTable.SheriffServices, "CRT_LOC_ID")]
    public void Validate_InvalidOrControlTarget_Throws(string targetTable, string targetColumn)
    {
        var mappingSet = MappingSet(Mapping(1, 100, targetTable, targetColumn));

        Assert.Throws<InvalidOperationException>(() => BiOracleStatMappingSeedValidator.Validate([mappingSet]));
    }

    [Fact]
    public async Task SeedAsync_MissingSubCategoryMetric_Throws()
    {
        await using var context = CreateContext();
        var seeder = Seeder(MappingSet(Mapping(1, 999, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync(context));
    }

    [Fact]
    public async Task SeedAsync_ArchivedSubCategoryMetric_Throws()
    {
        await using var context = CreateContext();
        context.SubCategoryMetrics.Add(new SubCategoryMetric { Id = 100, IsArchived = true });
        await context.SaveChangesAsync();
        var seeder = Seeder(MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync(context));
    }

    [Fact]
    public async Task SeedAsync_ValidMapping_PersistsMappingSetAndMapping()
    {
        await using var context = CreateContext();
        context.SubCategoryMetrics.Add(new SubCategoryMetric { Id = 100 });
        await context.SaveChangesAsync();
        var seeder = Seeder(MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1")));

        await seeder.SeedAsync(context);

        var mappingSet = await context.BiOracleStatMappingSets.SingleAsync();
        var mapping = await context.BiOracleStatMappings.SingleAsync();
        Assert.Equal(new DateOnly(2026, 1, 1), mappingSet.EffectiveDate);
        Assert.Equal(100, mapping.SubCategoryMetricId);
        Assert.Equal("GRD_ESC_HRS_L1", mapping.TargetColumn);
    }

    [Fact]
    public async Task SeedAsync_RuntimeEffectiveDate_UsesUtcDateOnFirstRunAndRetainsItOnRerun()
    {
        await using var context = CreateContext();
        context.SubCategoryMetrics.Add(new SubCategoryMetric { Id = 100 });
        await context.SaveChangesAsync();
        var definition = MappingSet(
            [Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1")],
            1,
            null,
            null
        );

        await Seeder(definition, new FixedTimeProvider(SeedTime)).SeedAsync(context);
        await Seeder(definition, new FixedTimeProvider(SeedTime.AddDays(10))).SeedAsync(context);

        var mappingSet = await context.BiOracleStatMappingSets.SingleAsync();
        Assert.Equal(new DateOnly(2026, 9, 26), mappingSet.EffectiveDate);
    }

    [Fact]
    public async Task SeedAsync_NewMappingForUsedMappingSet_Throws()
    {
        await using var context = CreateContext();
        context.SubCategoryMetrics.AddRange(new SubCategoryMetric { Id = 100 }, new SubCategoryMetric { Id = 101 });
        context.BiOracleStatMappingSets.Add(
            new BiOracleStatMappingSet { Id = 1, EffectiveDate = new DateOnly(2026, 1, 1) }
        );
        context.BiOracleStatMappings.Add(
            new BiOracleStatMapping
            {
                Id = 1,
                MappingSetId = 1,
                SubCategoryMetricId = 100,
                TargetTable = BiOracleTargetTable.SheriffServices,
                TargetColumn = "GRD_ESC_HRS_L1",
            }
        );
        context.BiOracleEtlRuns.Add(
            new BiOracleEtlRun
            {
                LoadId = Guid.NewGuid(),
                MappingSetId = 1,
                ReportingMonth = new DateOnly(2026, 9, 1),
                StartedAt = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero),
            }
        );
        await context.SaveChangesAsync();
        var seeder = Seeder(
            MappingSet(
                Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1"),
                Mapping(2, 101, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_REG_HRS_L1")
            )
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync(context));
    }

    [Fact]
    public async Task SeedAsync_IdenticalMappingsInNewContext_SucceedsWithoutChanges()
    {
        var databaseName = $"BiOracleSeed-{Guid.NewGuid()}";
        var definition = MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1"));
        await using (var context = CreateContext(databaseName))
        {
            context.SubCategoryMetrics.Add(new SubCategoryMetric { Id = 100 });
            await context.SaveChangesAsync();
            await Seeder(definition).SeedAsync(context);
        }

        await using var secondContext = CreateContext(databaseName);
        await Seeder(definition).SeedAsync(secondContext);

        Assert.Single(await secondContext.BiOracleStatMappingSets.ToArrayAsync());
        Assert.Single(await secondContext.BiOracleStatMappings.ToArrayAsync());
    }

    [Fact]
    public async Task SeedAsync_NewMappingForPersistedSetWithoutEtlRuns_Throws()
    {
        var databaseName = $"BiOracleSeed-{Guid.NewGuid()}";
        await using (var initialContext = CreateContext(databaseName))
        {
            initialContext.SubCategoryMetrics.AddRange(
                new SubCategoryMetric { Id = 100 },
                new SubCategoryMetric { Id = 101 }
            );
            await initialContext.SaveChangesAsync();
            await Seeder(MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1")))
                .SeedAsync(initialContext);
        }

        await using var context = CreateContext(databaseName);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Seeder(
                    MappingSet(
                        Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1"),
                        Mapping(2, 101, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_REG_HRS_L1")
                    )
                )
                .SeedAsync(context)
        );

        Assert.Contains("persisted", exception.Message);
        Assert.Single(await context.BiOracleStatMappings.ToArrayAsync());
    }

    [Fact]
    public async Task SeedAsync_PersistedSetWithoutMappings_RejectsPopulation()
    {
        var databaseName = $"BiOracleSeed-{Guid.NewGuid()}";
        await using (var initialContext = CreateContext(databaseName))
        {
            initialContext.SubCategoryMetrics.Add(new SubCategoryMetric { Id = 100 });
            initialContext.BiOracleStatMappingSets.Add(
                new BiOracleStatMappingSet { Id = 1, EffectiveDate = new DateOnly(2026, 1, 1) }
            );
            await initialContext.SaveChangesAsync();
        }

        await using var context = CreateContext(databaseName);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Seeder(MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1")))
                .SeedAsync(context)
        );

        Assert.Contains("persisted", exception.Message);
        Assert.Empty(await context.BiOracleStatMappings.ToArrayAsync());
    }

    [Fact]
    public async Task SeedAsync_RemovedMappingFromPersistedSet_Throws()
    {
        var databaseName = $"BiOracleSeed-{Guid.NewGuid()}";
        await using (var initialContext = CreateContext(databaseName))
        {
            initialContext.SubCategoryMetrics.AddRange(
                new SubCategoryMetric { Id = 100 },
                new SubCategoryMetric { Id = 101 }
            );
            await initialContext.SaveChangesAsync();
            await Seeder(
                    MappingSet(
                        Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1"),
                        Mapping(2, 101, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_REG_HRS_L1")
                    )
                )
                .SeedAsync(initialContext);
        }

        await using var context = CreateContext(databaseName);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Seeder(MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1")))
                .SeedAsync(context)
        );

        Assert.Contains("historical mappings absent", exception.Message);
        Assert.Equal(2, await context.BiOracleStatMappings.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_ChangedMappingInPersistedSet_Throws()
    {
        var databaseName = $"BiOracleSeed-{Guid.NewGuid()}";
        await using (var initialContext = CreateContext(databaseName))
        {
            initialContext.SubCategoryMetrics.Add(new SubCategoryMetric { Id = 100 });
            await initialContext.SaveChangesAsync();
            await Seeder(MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1")))
                .SeedAsync(initialContext);
        }

        await using var context = CreateContext(databaseName);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Seeder(MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L2")))
                .SeedAsync(context)
        );

        Assert.Contains("immutable", exception.Message);
        Assert.Equal("GRD_ESC_HRS_L1", (await context.BiOracleStatMappings.SingleAsync()).TargetColumn);
    }

    [Fact]
    public async Task SeedAsync_UsedSetAfterEtlRunsDeleted_RejectsNewMapping()
    {
        var databaseName = $"BiOracleSeed-{Guid.NewGuid()}";
        await using (var initialContext = CreateContext(databaseName))
        {
            initialContext.SubCategoryMetrics.AddRange(
                new SubCategoryMetric { Id = 100 },
                new SubCategoryMetric { Id = 101 }
            );
            await initialContext.SaveChangesAsync();
            await Seeder(MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1")))
                .SeedAsync(initialContext);
            var run = new BiOracleEtlRun
            {
                LoadId = Guid.NewGuid(),
                MappingSetId = 1,
                ReportingMonth = new DateOnly(2026, 9, 1),
                StartedAt = SeedTime,
            };
            initialContext.BiOracleEtlRuns.Add(run);
            await initialContext.SaveChangesAsync();
            initialContext.BiOracleEtlRuns.Remove(run);
            await initialContext.SaveChangesAsync();
        }

        await using var context = CreateContext(databaseName);
        Assert.Empty(await context.BiOracleEtlRuns.ToArrayAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Seeder(
                    MappingSet(
                        Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1"),
                        Mapping(2, 101, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_REG_HRS_L1")
                    )
                )
                .SeedAsync(context)
        );
        Assert.Single(await context.BiOracleStatMappings.ToArrayAsync());
    }

    [Fact]
    public async Task SeedAsync_NewMappingSetWithNewMappings_Succeeds()
    {
        var databaseName = $"BiOracleSeed-{Guid.NewGuid()}";
        var firstMapping = Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1");
        await using (var initialContext = CreateContext(databaseName))
        {
            initialContext.SubCategoryMetrics.AddRange(
                new SubCategoryMetric { Id = 100 },
                new SubCategoryMetric { Id = 101 }
            );
            await initialContext.SaveChangesAsync();
            await Seeder(MappingSet(firstMapping)).SeedAsync(initialContext);
        }

        await using var context = CreateContext(databaseName);
        await SeederForSets(
                MappingSet([firstMapping], 1, new DateOnly(2026, 1, 1), new DateOnly(2026, 8, 31)),
                MappingSet(
                    [Mapping(2, 101, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_REG_HRS_L1")],
                    2,
                    new DateOnly(2026, 9, 1),
                    null
                )
            )
            .SeedAsync(context);

        Assert.Equal(2, await context.BiOracleStatMappingSets.CountAsync());
        Assert.Equal(2, await context.BiOracleStatMappings.CountAsync());
        Assert.Equal(2, (await context.BiOracleStatMappings.SingleAsync(mapping => mapping.Id == 2)).MappingSetId);
        Assert.Equal(
            new DateOnly(2026, 8, 31),
            (await context.BiOracleStatMappingSets.SingleAsync(mappingSet => mappingSet.Id == 1)).ExpiryDate
        );
    }

    [Fact]
    public async Task SeedAsync_ExistingMappingWithArchivedMetric_Succeeds()
    {
        var databaseName = $"BiOracleSeed-{Guid.NewGuid()}";
        var definition = MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1"));
        await using (var context = CreateContext(databaseName))
        {
            context.SubCategoryMetrics.Add(new SubCategoryMetric { Id = 100 });
            await context.SaveChangesAsync();
            await Seeder(definition).SeedAsync(context);
            (await context.SubCategoryMetrics.SingleAsync()).IsArchived = true;
            await context.SaveChangesAsync();
        }

        await using var secondContext = CreateContext(databaseName);
        await Seeder(definition).SeedAsync(secondContext);

        Assert.Equal(100, (await secondContext.BiOracleStatMappings.SingleAsync()).SubCategoryMetricId);
        Assert.True((await secondContext.SubCategoryMetrics.SingleAsync()).IsArchived);
    }

    [Fact]
    public async Task SeedAsync_ExpiredMappingSetWithArchivedMetric_Succeeds()
    {
        var databaseName = $"BiOracleSeed-{Guid.NewGuid()}";
        var historicalSet = MappingSet(
            [Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1")],
            1,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 8, 31)
        );
        var currentSet = MappingSet(
            [Mapping(2, 101, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_REG_HRS_L1")],
            2,
            new DateOnly(2026, 9, 1),
            null
        );
        await using (var initialContext = CreateContext(databaseName))
        {
            initialContext.SubCategoryMetrics.AddRange(
                new SubCategoryMetric { Id = 100 },
                new SubCategoryMetric { Id = 101 }
            );
            await initialContext.SaveChangesAsync();
            await SeederForSets(historicalSet, currentSet).SeedAsync(initialContext);
            (await initialContext.SubCategoryMetrics.SingleAsync(metric => metric.Id == 100)).IsArchived = true;
            await initialContext.SaveChangesAsync();
        }

        await using var context = CreateContext(databaseName);
        await SeederForSets(historicalSet, currentSet).SeedAsync(context);

        Assert.Equal(2, await context.BiOracleStatMappings.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_ExistingMappingWithMissingMetric_Throws()
    {
        var databaseName = $"BiOracleSeed-{Guid.NewGuid()}";
        var definition = MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1"));
        await using (var initialContext = CreateContext(databaseName))
        {
            initialContext.SubCategoryMetrics.Add(new SubCategoryMetric { Id = 100 });
            await initialContext.SaveChangesAsync();
            await Seeder(definition).SeedAsync(initialContext);
        }
        await using (var deletionContext = CreateContext(databaseName))
        {
            deletionContext.SubCategoryMetrics.Remove(new SubCategoryMetric { Id = 100 });
            await deletionContext.SaveChangesAsync();
        }

        await using var context = CreateContext(databaseName);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Seeder(definition).SeedAsync(context)
        );

        Assert.Contains("missing SubCategoryMetric", exception.Message);
    }

    private static BiOracleStatMappingSeeder Seeder(
        BiOracleStatMappingSetSeedDefinition definition,
        TimeProvider? timeProvider = null
    ) =>
        new(
            NullLogger<BiOracleStatMappingSeeder>.Instance,
            [new BiOracleStatMappingSeedConfiguration { Source = "Test", Definitions = [definition] }],
            timeProvider
        );

    private static BiOracleStatMappingSeeder SeederForSets(params BiOracleStatMappingSetSeedDefinition[] definitions) =>
        new(
            NullLogger<BiOracleStatMappingSeeder>.Instance,
            [new BiOracleStatMappingSeedConfiguration { Source = "Test", Definitions = definitions }]
        );

    private static UnifiedDbContext CreateContext(string? databaseName = null) =>
        new(
            new DbContextOptionsBuilder<UnifiedDbContext>()
                .UseInMemoryDatabase(databaseName ?? $"BiOracleSeed-{Guid.NewGuid()}")
                .Options
        );

    private static BiOracleStatMappingSetSeedDefinition MappingSet(
        params BiOracleStatMappingSeedDefinition[] mappings
    ) => MappingSet(mappings, 1, new DateOnly(2026, 1, 1), null);

    private static BiOracleStatMappingSetSeedDefinition MappingSet(
        IReadOnlyList<BiOracleStatMappingSeedDefinition> mappings,
        int id,
        DateOnly? effectiveDate,
        DateOnly? expiryDate
    ) =>
        new()
        {
            Id = id,
            EffectiveDate = effectiveDate,
            ExpiryDate = expiryDate,
            Mappings = mappings,
        };

    private static BiOracleStatMappingSeedDefinition Mapping(
        int id,
        int subCategoryMetricId,
        string targetTable,
        string targetColumn
    ) =>
        new()
        {
            Id = id,
            SubCategoryMetricId = subCategoryMetricId,
            TargetTable = targetTable,
            TargetColumn = targetColumn,
        };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
