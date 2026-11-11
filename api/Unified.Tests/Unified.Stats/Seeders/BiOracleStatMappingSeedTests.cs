using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Db;
using Unified.Db.Models.Stats;
using Unified.Stats.Seeders;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.Stats.Seeders;

public sealed class BiOracleStatMappingSeedTests
{
    private static readonly DateTimeOffset SeedTime = new(2026, 9, 26, 23, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ProductionData_OriginalMappingIds_RetainTheirSourceAndTargetAssociations()
    {
        var mappings = Assert.Single(BiOracleStatMappingSeedData.Definitions).Mappings
            .Where(mapping => mapping.Id <= 516)
            .OrderBy(mapping => mapping.Id)
            .ToArray();
        Assert.Equal(Enumerable.Range(1, 516), mappings.Select(mapping => mapping.Id));
        var associations = string.Join(
            "\n",
            mappings.Select(mapping =>
                $"{mapping.Id}|{mapping.SubCategoryMetricId}|{mapping.TargetTable}|{mapping.TargetColumn}"
            )
        );
        // Fingerprint of the original 516-row application seed in commit ed962694.
        Assert.Equal(
            "69E1F4457C2CF045D6F3086BFA48252AC81F67D5F839384CF165937F08E63C2E",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(associations)))
        );
    }

    [Fact]
    public void ProductionData_HasExpectedMappingsAndCorrectedOthersTaxonomy()
    {
        var mappingSet = Assert.Single(BiOracleStatMappingSeedData.Definitions);

        Assert.Equal(1, mappingSet.Id);
        Assert.Equal(new DateOnly(2026, 11, 1), mappingSet.EffectiveDate);
        Assert.Null(mappingSet.ExpiryDate);
        Assert.Equal(661, mappingSet.Mappings.Count);
        Assert.Equal(Enumerable.Range(1, 661), mappingSet.Mappings.Select(mapping => mapping.Id));
        Assert.Equal(
            296,
            mappingSet.Mappings.Select(mapping => (mapping.TargetTable, mapping.TargetColumn)).Distinct().Count()
        );
        BiOracleStatMappingSeedValidator.Validate(BiOracleStatMappingSeedData.Definitions);
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
        var mappings = Assert
            .Single(BiOracleStatMappingSeedData.Definitions, definition => definition.Id == 1)
            .Mappings;
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
            (435, "CVF_DOCS_RCVD"),
            (436, "CVF_DOCS_CONC"),
            (437, "CVF_OTHR_RCVD"),
            (438, "CVF_OTHR_CONC"),
            (439, "CVF_WARR_RCVD"),
            (440, "CVF_WARR_CONC"),
            (441, "CRIM_CRTORD_RCVD"),
            (442, "CRIM_CRTORD_CONC"),
            (443, "CRIM_DOCS_RCVD"),
            (444, "CRIM_DOCS_CONC"),
            (445, "CRIM_OTHR_RCVD"),
            (446, "CRIM_OTHR_CONC"),
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
    public void Validate_DifferentMappingSetIdsWithSameEffectiveDate_Throws()
    {
        var effectiveDate = new DateOnly(2026, 1, 1);
        var first = MappingSet([], id: 1, effectiveDate, expiryDate: effectiveDate);
        var second = MappingSet([], id: 2, effectiveDate, expiryDate: null);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            BiOracleStatMappingSeedValidator.Validate([first, second])
        );

        Assert.Contains("Duplicate BiOracle mapping-set effective date", exception.Message);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(31)]
    public void Validate_NonOverlappingHistoricalAndCurrentPeriods_DoesNotThrow(int expiryDay)
    {
        var historical = MappingSet(
            [],
            id: 1,
            effectiveDate: new DateOnly(2026, 1, 1),
            expiryDate: new DateOnly(2026, 8, expiryDay)
        );
        var current = MappingSet([], id: 2, effectiveDate: new DateOnly(2026, 9, 1), expiryDate: null);

        BiOracleStatMappingSeedValidator.Validate([current, historical]);
    }

    [Fact]
    public void Validate_OverlappingMappingSets_Throws()
    {
        var first = MappingSet(
            [],
            id: 1,
            effectiveDate: new DateOnly(2026, 1, 1),
            expiryDate: new DateOnly(2026, 9, 1)
        );
        var second = MappingSet([], id: 2, effectiveDate: new DateOnly(2026, 9, 1), expiryDate: null);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            BiOracleStatMappingSeedValidator.Validate([first, second])
        );

        Assert.Contains("overlapping effective periods", exception.Message);
    }

    [Fact]
    public void Validate_MultipleUnexpiredMappingSets_Throws()
    {
        var first = MappingSet([], id: 1, effectiveDate: new DateOnly(2026, 1, 1), expiryDate: null);
        var second = MappingSet([], id: 2, effectiveDate: new DateOnly(2026, 9, 1), expiryDate: null);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            BiOracleStatMappingSeedValidator.Validate([first, second])
        );

        Assert.Contains("exactly one unexpired mapping set", exception.Message);
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
    [InlineData(BiOracleTargetTable.SheriffServices, "NIL_REPORT")]
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

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            seeder.SeedAsync(context, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task SeedAsync_ArchivedSubCategoryMetric_Throws()
    {
        await using var context = CreateContext();
        context.SubCategoryMetrics.Add(new SubCategoryMetric { Id = 100, IsArchived = true });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var seeder = Seeder(MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1")));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            seeder.SeedAsync(context, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task SeedAsync_ValidMapping_PersistsMappingSetAndMapping()
    {
        await using var context = CreateContext();
        context.SubCategoryMetrics.Add(new SubCategoryMetric { Id = 100 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var seeder = Seeder(MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1")));

        await seeder.SeedAsync(context, TestContext.Current.CancellationToken);

        var mappingSet = await context.BiOracleStatMappingSets.SingleAsync(
            cancellationToken: TestContext.Current.CancellationToken
        );
        var mapping = await context.BiOracleStatMappings.SingleAsync(
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(new DateOnly(2026, 1, 1), mappingSet.EffectiveDate);
        Assert.Equal(100, mapping.SubCategoryMetricId);
        Assert.Equal("GRD_ESC_HRS_L1", mapping.TargetColumn);
    }

    [Fact]
    public async Task SeedAsync_RuntimeEffectiveDate_UsesUtcDateOnFirstRunAndRetainsItOnRerun()
    {
        await using var context = CreateContext();
        context.SubCategoryMetrics.Add(new SubCategoryMetric { Id = 100 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var definition = MappingSet(
            [Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1")],
            1,
            null,
            null
        );

        await Seeder(definition, new FixedTimeProvider(SeedTime))
            .SeedAsync(context, TestContext.Current.CancellationToken);
        await Seeder(definition, new FixedTimeProvider(SeedTime.AddDays(10)))
            .SeedAsync(context, TestContext.Current.CancellationToken);

        var mappingSet = await context.BiOracleStatMappingSets.SingleAsync(
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(new DateOnly(2026, 9, 26), mappingSet.EffectiveDate);
    }

    [Theory]
    [InlineData(BiOracleEtlRunStatus.Running)]
    [InlineData(BiOracleEtlRunStatus.Succeeded)]
    [InlineData(BiOracleEtlRunStatus.Failed)]
    public async Task SeedAsync_NewMappingForUsedMappingSet_Throws(string status)
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
                Status = status,
            }
        );
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var seeder = Seeder(
            MappingSet(
                Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1"),
                Mapping(2, 101, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_REG_HRS_L1")
            )
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            seeder.SeedAsync(context, TestContext.Current.CancellationToken)
        );
        Assert.Contains("has ETL history", exception.Message);
        Assert.Single(await context.BiOracleStatMappings.ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, (await context.BiOracleEtlRuns.SingleAsync(TestContext.Current.CancellationToken)).MappingSetId);
    }

    [Fact]
    public async Task SeedAsync_IdenticalMappingsInNewContext_SucceedsWithoutChanges()
    {
        var databaseName = $"BiOracleSeed-{Guid.NewGuid()}";
        var definition = MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1"));
        await using (var context = CreateContext(databaseName))
        {
            context.SubCategoryMetrics.Add(new SubCategoryMetric { Id = 100 });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            await Seeder(definition).SeedAsync(context, TestContext.Current.CancellationToken);
        }

        await using var secondContext = CreateContext(databaseName);
        await Seeder(definition).SeedAsync(secondContext, TestContext.Current.CancellationToken);

        var mappingSet = Assert.Single(
            await secondContext.BiOracleStatMappingSets.ToArrayAsync(
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(definition.EffectiveDate, mappingSet.EffectiveDate);
        Assert.Single(
            await secondContext.BiOracleStatMappings.ToArrayAsync(
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task SeedAsync_NewMappingForPersistedSetWithoutEtlRuns_InsertsMissingMapping()
    {
        var databaseName = $"BiOracleSeed-{Guid.NewGuid()}";
        await using (var initialContext = CreateContext(databaseName))
        {
            initialContext.SubCategoryMetrics.AddRange(
                new SubCategoryMetric { Id = 100 },
                new SubCategoryMetric { Id = 101 }
            );
            await initialContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            await Seeder(MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1")))
                .SeedAsync(initialContext, TestContext.Current.CancellationToken);
        }

        await using var context = CreateContext(databaseName);
        await Seeder(
            MappingSet(
                Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1"),
                Mapping(2, 101, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_REG_HRS_L1")
            )
        ).SeedAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(2, await context.BiOracleStatMappings.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SeedAsync_PersistedSetWithoutMappingsOrEtlRuns_PopulatesMappings()
    {
        var databaseName = $"BiOracleSeed-{Guid.NewGuid()}";
        await using (var initialContext = CreateContext(databaseName))
        {
            initialContext.SubCategoryMetrics.Add(new SubCategoryMetric { Id = 100 });
            initialContext.BiOracleStatMappingSets.Add(
                new BiOracleStatMappingSet { Id = 1, EffectiveDate = new DateOnly(2026, 1, 1) }
            );
            await initialContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = CreateContext(databaseName);
        await Seeder(MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1")))
            .SeedAsync(context, TestContext.Current.CancellationToken);

        Assert.Single(
            await context.BiOracleStatMappings.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken)
        );
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
            await initialContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            await Seeder(
                    MappingSet(
                        Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1"),
                        Mapping(2, 101, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_REG_HRS_L1")
                    )
                )
                .SeedAsync(initialContext, TestContext.Current.CancellationToken);
        }

        await using var context = CreateContext(databaseName);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Seeder(MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1")))
                .SeedAsync(context, TestContext.Current.CancellationToken)
        );

        Assert.Contains("historical mappings absent", exception.Message);
        Assert.Equal(
            2,
            await context.BiOracleStatMappings.CountAsync(cancellationToken: TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task SeedAsync_ChangedMappingInPersistedSet_Throws()
    {
        var databaseName = $"BiOracleSeed-{Guid.NewGuid()}";
        await using (var initialContext = CreateContext(databaseName))
        {
            initialContext.SubCategoryMetrics.Add(new SubCategoryMetric { Id = 100 });
            await initialContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            await Seeder(MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1")))
                .SeedAsync(initialContext, TestContext.Current.CancellationToken);
        }

        await using var context = CreateContext(databaseName);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Seeder(MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L2")))
                .SeedAsync(context, TestContext.Current.CancellationToken)
        );

        Assert.Contains("immutable", exception.Message);
        Assert.Equal(
            "GRD_ESC_HRS_L1",
            (
                await context.BiOracleStatMappings.SingleAsync(cancellationToken: TestContext.Current.CancellationToken)
            ).TargetColumn
        );
    }

    [Fact]
    public async Task SeedAsync_WhenNoEtlReferencesRemain_PermitsAdditiveSeeding()
    {
        var databaseName = $"BiOracleSeed-{Guid.NewGuid()}";
        await using (var initialContext = CreateContext(databaseName))
        {
            initialContext.SubCategoryMetrics.AddRange(
                new SubCategoryMetric { Id = 100 },
                new SubCategoryMetric { Id = 101 }
            );
            await initialContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            await Seeder(MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1")))
                .SeedAsync(initialContext, TestContext.Current.CancellationToken);
            var run = new BiOracleEtlRun
            {
                LoadId = Guid.NewGuid(),
                MappingSetId = 1,
                ReportingMonth = new DateOnly(2026, 9, 1),
                StartedAt = SeedTime,
            };
            initialContext.BiOracleEtlRuns.Add(run);
            await initialContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            initialContext.BiOracleEtlRuns.Remove(run);
            await initialContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = CreateContext(databaseName);
        Assert.Empty(
            await context.BiOracleEtlRuns.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken)
        );
        await Seeder(
            MappingSet(
                Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1"),
                Mapping(2, 101, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_REG_HRS_L1")
            )
        ).SeedAsync(context, TestContext.Current.CancellationToken);
        Assert.Equal(2, await context.BiOracleStatMappings.CountAsync(TestContext.Current.CancellationToken));
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
            await initialContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            await Seeder(MappingSet(firstMapping)).SeedAsync(initialContext, TestContext.Current.CancellationToken);
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
            .SeedAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(
            2,
            await context.BiOracleStatMappingSets.CountAsync(cancellationToken: TestContext.Current.CancellationToken)
        );
        Assert.Equal(
            2,
            await context.BiOracleStatMappings.CountAsync(cancellationToken: TestContext.Current.CancellationToken)
        );
        Assert.Equal(
            2,
            (
                await context.BiOracleStatMappings.SingleAsync(
                    mapping => mapping.Id == 2,
                    cancellationToken: TestContext.Current.CancellationToken
                )
            ).MappingSetId
        );
        Assert.Equal(
            new DateOnly(2026, 8, 31),
            (
                await context.BiOracleStatMappingSets.SingleAsync(
                    mappingSet => mappingSet.Id == 1,
                    cancellationToken: TestContext.Current.CancellationToken
                )
            ).ExpiryDate
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
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            await Seeder(definition).SeedAsync(context, TestContext.Current.CancellationToken);
            (
                await context.SubCategoryMetrics.SingleAsync(cancellationToken: TestContext.Current.CancellationToken)
            ).IsArchived = true;
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var secondContext = CreateContext(databaseName);
        await Seeder(definition).SeedAsync(secondContext, TestContext.Current.CancellationToken);

        Assert.Equal(
            100,
            (
                await secondContext.BiOracleStatMappings.SingleAsync(
                    cancellationToken: TestContext.Current.CancellationToken
                )
            ).SubCategoryMetricId
        );
        Assert.True(
            (
                await secondContext.SubCategoryMetrics.SingleAsync(
                    cancellationToken: TestContext.Current.CancellationToken
                )
            ).IsArchived
        );
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
            await initialContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            await SeederForSets(historicalSet, currentSet)
                .SeedAsync(initialContext, TestContext.Current.CancellationToken);
            (
                await initialContext.SubCategoryMetrics.SingleAsync(
                    metric => metric.Id == 100,
                    cancellationToken: TestContext.Current.CancellationToken
                )
            ).IsArchived = true;
            await initialContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = CreateContext(databaseName);
        await SeederForSets(historicalSet, currentSet).SeedAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(
            2,
            await context.BiOracleStatMappings.CountAsync(cancellationToken: TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task SeedAsync_ExistingMappingWithMissingMetric_Throws()
    {
        var databaseName = $"BiOracleSeed-{Guid.NewGuid()}";
        var definition = MappingSet(Mapping(1, 100, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1"));
        await using (var initialContext = CreateContext(databaseName))
        {
            initialContext.SubCategoryMetrics.Add(new SubCategoryMetric { Id = 100 });
            await initialContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            await Seeder(definition).SeedAsync(initialContext, TestContext.Current.CancellationToken);
        }
        await using (var deletionContext = CreateContext(databaseName))
        {
            deletionContext.SubCategoryMetrics.Remove(new SubCategoryMetric { Id = 100 });
            await deletionContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = CreateContext(databaseName);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Seeder(definition).SeedAsync(context, TestContext.Current.CancellationToken)
        );

        Assert.Contains("missing SubCategoryMetric", exception.Message);
    }

    [Theory]
    [InlineData(0, 11, 1)]
    [InlineData(516, 11, 1)]
    [InlineData(516, 10, 27)]
    public async Task SeedAsync_ProductionMappings_CreatesOrExtendsSetWithoutChangingExistingRows(
        int existingCount,
        int month,
        int day
    )
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(ct);
        await using var context = CreateRelationalContext(connection);
        await context.Database.EnsureCreatedAsync(ct);
        await SeedTaxonomyAsync(context);
        var definition = Assert.Single(BiOracleStatMappingSeedData.Definitions);
        var existingDate = new DateOnly(2026, month, day);
        if (existingCount > 0)
        {
            await Seeder(definition with
            {
                EffectiveDate = existingDate,
                Mappings = definition.Mappings.Take(existingCount).ToArray(),
            }).SeedAsync(context, ct);
        }
        var originalRows = await context.BiOracleStatMappings.AsNoTracking().OrderBy(mapping => mapping.Id)
            .Select(mapping => new
            {
                mapping.Id,
                mapping.MappingSetId,
                mapping.SubCategoryMetricId,
                mapping.TargetTable,
                mapping.TargetColumn,
                mapping.CreatedOn,
                mapping.UpdatedOn,
            }).ToArrayAsync(ct);
        Assert.Equal(existingCount, originalRows.Length);
        context.ChangeTracker.Clear();

        await Seeder(definition).SeedAsync(context, ct);

        var set = await context.BiOracleStatMappingSets.SingleAsync(ct);
        Assert.Equal(1, set.Id);
        Assert.Equal(existingCount == 0 ? definition.EffectiveDate : existingDate, set.EffectiveDate);
        Assert.Null(set.ExpiryDate);
        var completeRows = await context.BiOracleStatMappings.AsNoTracking().OrderBy(mapping => mapping.Id)
            .Select(mapping => new
            {
                mapping.Id,
                mapping.MappingSetId,
                mapping.SubCategoryMetricId,
                mapping.TargetTable,
                mapping.TargetColumn,
                mapping.CreatedOn,
                mapping.UpdatedOn,
            }).ToArrayAsync(ct);
        Assert.Equal(661, completeRows.Length);
        Assert.Equal(originalRows, completeRows.Take(existingCount).ToArray());
        Assert.Equal(
            definition.Mappings.OrderBy(mapping => mapping.Id).Select(mapping =>
                (mapping.Id, definition.Id, mapping.SubCategoryMetricId, mapping.TargetTable, mapping.TargetColumn)),
            completeRows.Select(mapping =>
                (mapping.Id, mapping.MappingSetId, mapping.SubCategoryMetricId, mapping.TargetTable, mapping.TargetColumn))
        );

        context.ChangeTracker.Clear();
        await Seeder(definition).SeedAsync(context, ct);
        Assert.Equal(set.EffectiveDate, (await context.BiOracleStatMappingSets.SingleAsync(ct)).EffectiveDate);
        Assert.Equal(1, await context.BiOracleStatMappingSets.CountAsync(ct));
        Assert.Equal(
            completeRows,
            await context.BiOracleStatMappings.AsNoTracking().OrderBy(mapping => mapping.Id)
                .Select(mapping => new
                {
                    mapping.Id,
                    mapping.MappingSetId,
                    mapping.SubCategoryMetricId,
                    mapping.TargetTable,
                    mapping.TargetColumn,
                    mapping.CreatedOn,
                    mapping.UpdatedOn,
                }).ToArrayAsync(ct)
        );
    }

    [Theory]
    [InlineData("definition")]
    [InlineData("other-set")]
    [InlineData("history")]
    [InlineData("archived")]
    [InlineData("overlap")]
    public async Task SeedAsync_UnsafeAdditiveUpdate_LeavesPersistedAndTrackedDataUnchanged(string conflict)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(ct);
        await using var context = CreateRelationalContext(connection);
        await context.Database.EnsureCreatedAsync(ct);
        await SeedTaxonomyAsync(context);
        var firstMapping = Mapping(1, 5, BiOracleTargetTable.SheriffServices, "CA_REG_HRS");
        var missingMapping = Mapping(2, 6, BiOracleTargetTable.SheriffServices, "CA_REG_OT_HRS");
        var originalDate = new DateOnly(2026, 10, 27);
        await Seeder(MappingSet([firstMapping], 1, originalDate, null)).SeedAsync(context, ct);
        if (conflict is "other-set" or "overlap")
        {
            context.BiOracleStatMappingSets.Add(new BiOracleStatMappingSet
            {
                Id = 2,
                EffectiveDate = new DateOnly(2026, 9, 1),
                ExpiryDate = conflict == "overlap" ? new DateOnly(2026, 10, 31) : new DateOnly(2026, 10, 26),
            });
            if (conflict == "other-set")
            {
                context.BiOracleStatMappings.Add(new BiOracleStatMapping
                {
                    Id = missingMapping.Id,
                    MappingSetId = 2,
                    SubCategoryMetricId = missingMapping.SubCategoryMetricId,
                    TargetTable = missingMapping.TargetTable,
                    TargetColumn = missingMapping.TargetColumn,
                });
            }
        }
        if (conflict == "history")
        {
            context.BiOracleEtlRuns.Add(new BiOracleEtlRun
            {
                LoadId = Guid.NewGuid(),
                MappingSetId = 1,
                ReportingMonth = new DateOnly(2026, 11, 1),
                StartedAt = SeedTime,
                Status = BiOracleEtlRunStatus.Succeeded,
            });
        }
        if (conflict == "archived")
            (await context.SubCategoryMetrics.SingleAsync(metric => metric.Id == 6, ct)).IsArchived = true;
        await context.SaveChangesAsync(ct);
        var originalMappingCount = await context.BiOracleStatMappings.CountAsync(ct);
        context.ChangeTracker.Clear();
        var requestedMapping = conflict == "definition"
            ? firstMapping with { TargetColumn = "CA_HSM_HRS" }
            : firstMapping;
        var definition = MappingSet([missingMapping, requestedMapping], 1, new DateOnly(2026, 11, 1), null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Seeder(definition).SeedAsync(context, ct));

        Assert.Empty(context.ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted));
        Assert.Equal(0, await context.SaveChangesAsync(ct));
        context.ChangeTracker.Clear();
        var retainedSet = await context.BiOracleStatMappingSets.SingleAsync(set => set.Id == 1, ct);
        Assert.Equal(originalDate, retainedSet.EffectiveDate);
        Assert.Null(retainedSet.ExpiryDate);
        Assert.Equal(originalMappingCount, await context.BiOracleStatMappings.CountAsync(ct));
        var retainedMapping = await context.BiOracleStatMappings.SingleAsync(mapping => mapping.Id == 1, ct);
        Assert.Equal(firstMapping.SubCategoryMetricId, retainedMapping.SubCategoryMetricId);
        Assert.Equal(firstMapping.TargetTable, retainedMapping.TargetTable);
        Assert.Equal(firstMapping.TargetColumn, retainedMapping.TargetColumn);
        if (conflict == "other-set")
            Assert.Equal(2, (await context.BiOracleStatMappings.SingleAsync(mapping => mapping.Id == 2, ct)).MappingSetId);
        if (conflict == "history")
            Assert.Equal(1, (await context.BiOracleEtlRuns.SingleAsync(ct)).MappingSetId);
    }

    private static UnifiedDbContext CreateRelationalContext(SqliteConnection connection) =>
        new SqliteTestUnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(connection).Options
        ) { AuditDisabled = true };

    private static async Task SeedTaxonomyAsync(UnifiedDbContext context)
    {
        var ct = TestContext.Current.CancellationToken;
        await new StatGroupSeeder(NullLogger<StatGroupSeeder>.Instance).SeedAsync(context, ct);
        await new StatCategorySeeder(NullLogger<StatCategorySeeder>.Instance).SeedAsync(context, ct);
        await new SubCategorySeeder(NullLogger<SubCategorySeeder>.Instance).SeedAsync(context, ct);
        await new StatMetricSeeder(NullLogger<StatMetricSeeder>.Instance).SeedAsync(context, ct);
        await new SubCategoryMetricSeeder(NullLogger<SubCategoryMetricSeeder>.Instance).SeedAsync(context, ct);
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
