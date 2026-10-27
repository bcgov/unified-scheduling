using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Db;
using Unified.Db.Models.Stats;
using Unified.Stats.Seeders;

namespace Unified.Tests.Stats.Seeders;

public sealed class LegacyBiOracleStatsSeedTests
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SeedAsync_FlatTaxonomyAndArchival_AreStableAcrossRuns()
    {
        await using var db = CreateContext();
        await SeedTaxonomyAsync(db);
        var originalCount = await db.SubCategoryMetrics.CountAsync(CancellationToken);
        db.StatRecords.Add(
            new StatRecord
            {
                Id = 1,
                DateFrom = new DateOnly(2026, 9, 1),
                DateTo = new DateOnly(2026, 9, 1),
                PeriodType = "Daily",
                LocationId = 1,
                SubCategoryMetricId = 408,
                Value = 37,
            }
        );
        (await db.StatCategories.SingleAsync(c => c.Id == 32, CancellationToken)).IsArchived = false;
        (await db.SubCategories.SingleAsync(c => c.Id == 106, CancellationToken)).IsArchived = false;
        (await db.SubCategoryMetrics.SingleAsync(c => c.Id == 408, CancellationToken)).IsArchived = false;
        await db.SaveChangesAsync(CancellationToken);
        await SeedTaxonomyAsync(db);

        string[] demographics =
        [
            "Male - Provincial Adult",
            "Male - Federal Adult",
            "Male - Youth",
            "Female - Provincial Adult",
            "Female - Federal Adult",
            "Female - Youth",
        ];
        (int CategoryId, int FirstId, int FirstScmId, string Summary, int[] Metrics)[] areas =
        [
            (29, 117, 449, "Hours", [33, 34]),
            (27, 123, 461, "Trips", [36, 38, 40, 42]),
            (28, 129, 485, "Trips & Kilometres", [37, 39, 41, 42]),
        ];
        foreach (var area in areas)
        {
            var subs = await db
                .SubCategories.Where(s => s.CategoryId == area.CategoryId && !s.IsArchived)
                .OrderBy(s => s.DisplayOrder)
                .ToArrayAsync(CancellationToken);
            Assert.Equal(new[] { area.Summary }.Concat(demographics), subs.Select(s => s.Name));
            for (var i = 0; i < 6; i++)
            {
                Assert.Equal(area.FirstId + i, subs[i + 1].Id);
                var scms = await db
                    .SubCategoryMetrics.Where(s => s.SubCategoryId == subs[i + 1].Id && !s.IsArchived)
                    .OrderBy(s => s.DisplayOrder)
                    .ToArrayAsync(CancellationToken);
                Assert.Equal(area.Metrics, scms.Select(s => s.MetricId));
                Assert.Equal(
                    Enumerable.Range(area.FirstScmId + i * area.Metrics.Length, area.Metrics.Length),
                    scms.Select(s => s.Id)
                );
            }
        }

        Assert.Equal(originalCount, await db.SubCategoryMetrics.CountAsync(CancellationToken));
        var historical = await db.StatRecords.SingleAsync(CancellationToken);
        Assert.Equal((408, 37m), (historical.SubCategoryMetricId, historical.Value!.Value));
        Assert.Equal(63, await db.SubCategoryMetrics.CountAsync(s => s.Id >= 449 && s.Id <= 511, CancellationToken));
        Assert.All(
            await db.StatCategories.Where(c => c.Id == 32 || c.Id == 33).ToArrayAsync(CancellationToken),
            c => Assert.True(c.IsArchived)
        );
        Assert.All(
            await db.SubCategories.Where(s => s.Id == 106 || s.Id == 107).ToArrayAsync(CancellationToken),
            s => Assert.True(s.IsArchived)
        );
        Assert.All(
            await db
                .SubCategoryMetrics.Where(s => (s.Id >= 408 && s.Id <= 413) || (s.Id >= 418 && s.Id <= 427))
                .ToArrayAsync(CancellationToken),
            s => Assert.True(s.IsArchived)
        );
        var hours = await db.SubCategoryMetrics.SingleAsync(s => s.Id == 407, CancellationToken);
        Assert.Equal((105, 14, false), (hours.SubCategoryId, hours.MetricId, hours.IsArchived));
        var circuit = await db.SubCategoryMetrics.SingleAsync(s => s.Id == 509, CancellationToken);
        Assert.Equal((92, 29), (circuit.SubCategoryId, circuit.MetricId));
        var supervision = await db.StatCategories.SingleAsync(c => c.Id == 37, CancellationToken);
        Assert.Equal(2, supervision.GroupId);
        Assert.Equal(
            new[] { 1, 2 },
            await db
                .SubCategoryMetrics.Where(s => s.SubCategoryId == 135)
                .OrderBy(s => s.DisplayOrder)
                .Select(s => s.MetricId)
                .ToArrayAsync(CancellationToken)
        );
    }

    [Fact]
    public async Task SeedAsync_NewSeedIdCollidesWithGeneratedRow_RejectsRepurposing()
    {
        await using var db = CreateContext();
        db.SubCategoryMetrics.Add(
            new SubCategoryMetric
            {
                Id = 500,
                SubCategoryId = 18,
                MetricId = 1,
            }
        );
        await db.SaveChangesAsync(CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SubCategoryMetricSeeder(NullLogger<SubCategoryMetricSeeder>.Instance).SeedAsync(db, CancellationToken)
        );
        Assert.Equal(
            18,
            (await db.SubCategoryMetrics.AsNoTracking().SingleAsync(s => s.Id == 500, CancellationToken)).SubCategoryId
        );
    }

    [Fact]
    public void ExpandedMappings_OriginalMappings_RetainTheirMeaningUnderDistinctKeys()
    {
        var original = BiOracleStatMappingSeedData.Definitions.Single(d => d.Id == 1);
        foreach (var mapping in original.Mappings)
        {
            var copied = Assert.Single(Expanded.Mappings, m => m.Id == mapping.Id + 516);
            Assert.Equal(
                (mapping.SubCategoryMetricId, mapping.TargetTable, mapping.TargetColumn),
                (copied.SubCategoryMetricId, copied.TargetTable, copied.TargetColumn)
            );
        }
        Assert.Empty(original.Mappings.Select(m => m.Id).Intersect(Expanded.Mappings.Select(m => m.Id)));
    }

    [Fact]
    public void ExpandedMappings_HoldingCells_MapEachClassificationIndependently()
    {
        string[] expected =
        [
            "HLD_AMAL_CNT",
            "SEG_AMAL_CNT",
            "FED_AMAL_CNT",
            "FED_SEG_AMAL_CNT",
            "HLD_YMAL_CNT",
            "SEG_YMAL_CNT",
            "HLD_AFEM_CNT",
            "SEG_AFEM_CNT",
            "FED_AFEM_CNT",
            "FED_SEG_AFEM_CNT",
            "HLD_YFEM_CNT",
            "SEG_YFEM_CNT",
        ];
        for (var i = 0; i < expected.Length; i++)
        {
            var mapping = Assert.Single(Expanded.Mappings.Where(m => m.SubCategoryMetricId == 449 + i));
            Assert.Equal(BiOracleTargetTable.SheriffServices, mapping.TargetTable);
            Assert.Equal(expected[i], mapping.TargetColumn);
        }
    }

    [Theory]
    [InlineData("AIR", 461)]
    [InlineData("GRD", 485)]
    public void ExpandedMappings_TransportCellsAndAutoSums_MatchWorkbookAndExcludeFederal(string mode, int firstId)
    {
        string[] codes = ["AMAL", "FMAL", "YMAL", "AFEM", "FFEM", "YFEM"];
        string[] levels = ["L1", "L2", "L3", "SEG_PC"];
        for (var demographic = 0; demographic < codes.Length; demographic++)
        {
            for (var level = 0; level < levels.Length; level++)
            {
                var expected = new List<string> { $"{mode}_{codes[demographic]}_CNT_{levels[level]}" };
                if (demographic is not (1 or 4))
                {
                    expected.Add($"{mode}_{codes[demographic]}_CNT");
                    if (level < 2)
                        expected.Add($"{mode}_LVL{level + 1}_CNT");
                }
                var mappings = Expanded
                    .Mappings.Where(m => m.SubCategoryMetricId == firstId + demographic * 4 + level)
                    .ToArray();
                Assert.Equal(expected.Order(), mappings.Select(m => m.TargetColumn).Order());
                Assert.All(mappings, m => Assert.Equal(BiOracleTargetTable.SheriffServices, m.TargetTable));
            }
        }

        // One contribution per Provincial/youth cell, with deliberately different values.
        var values = Enumerable.Range(firstId, 24).ToDictionary(id => id, id => (decimal)(id - firstId + 1));
        foreach (var level in new[] { 0, 1 })
        {
            var expected = new[] { 0, 2, 3, 5 }.Sum(d => values[firstId + d * 4 + level]);
            var actual = Expanded
                .Mappings.Where(m => m.TargetColumn == $"{mode}_LVL{level + 1}_CNT")
                .Sum(m => values[m.SubCategoryMetricId]);
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void ExpandedMappings_DocumentsAndSupervision_UseSeparateBusinessMeanings()
    {
        (int Id, string Column)[] documents =
        [
            (435, "CVF_DOCS_RCVD"),
            (436, "CVF_DOCS_CONC"),
            (437, "CVF_OTHR_RCVD"),
            (438, "CVF_OTHR_CONC"),
            (443, "CRIM_DOCS_RCVD"),
            (444, "CRIM_DOCS_CONC"),
            (445, "CRIM_OTHR_RCVD"),
            (446, "CRIM_OTHR_CONC"),
        ];
        foreach (var document in documents)
        {
            var mapping = Assert.Single(Expanded.Mappings.Where(m => m.SubCategoryMetricId == document.Id));
            Assert.Equal(document.Column, mapping.TargetColumn);
            Assert.Equal(BiOracleTargetTable.SheriffServices, mapping.TargetTable);
        }
        Assert.Equal(new[] { 510, 511 }, Sources("SUP_DUTIES_N_FRONT_LINE"));
        Assert.Equal(new[] { 510 }, Sources("SUP_DUTIES_N_FRONT_LINE_REG"));
        Assert.Equal(new[] { 511 }, Sources("SUP_DUTIES_N_FRONT_LINE_OT"));
        Assert.All(
            Expanded.Mappings.Where(m => m.TargetColumn.EndsWith("_REG") || m.TargetColumn.EndsWith("_OT")),
            m => Assert.Equal(BiOracleTargetTable.SheriffServicesHours, m.TargetTable)
        );
        Assert.Equal(new[] { 509 }, Sources("CCRT_TRIPS_CNT"));
        Assert.Equal(new[] { 407 }, Sources("HLD_CELL_HRS"));
        Assert.Equal(new[] { 375, 376, 377 }, Sources("GRD_KM_TRAVL"));
        Assert.Equal(new[] { 112, 113, 114, 115, 116, 117, 316, 317, 318, 319, 320, 321 }, Sources("AIR_SHRF_HRS"));
        Assert.Equal(new[] { 124, 125, 126, 127, 128, 129, 325, 326, 327, 328, 329, 330 }, Sources("GRD_SHRF_HRS"));
        Assert.DoesNotContain(Expanded.Mappings, m => m.SubCategoryMetricId is >= 408 and <= 413 or >= 418 and <= 427);
        Assert.Equal(
            Expanded.Mappings.Count,
            Expanded.Mappings.Select(m => (m.SubCategoryMetricId, m.TargetTable, m.TargetColumn)).Distinct().Count()
        );
    }

    [Theory]
    [InlineData(2026, 10, 27)]
    [InlineData(2026, 12, 15)]
    public async Task SeedAsync_ProductionCutover_PreservesSetOneAndSupportsFreshAndRepeatedSeeding(
        int year,
        int month,
        int day
    )
    {
        await using var db = CreateContext();
        await SeedTaxonomyAsync(db);
        var clock = new FixedTimeProvider(new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.Zero));
        var original = BiOracleStatMappingSeedData.Definitions.Single(d => d.Id == 1);
        var expectedInitialDate = new DateOnly(year, month, day);
        if (expectedInitialDate > original.ExpiryDate!.Value)
            expectedInitialDate = original.ExpiryDate.Value;
        // Simulate the already-deployed set 1, with no new mappings added to it.
        await MappingSeeder([original with { ExpiryDate = null, EffectiveDate = expectedInitialDate }], clock)
            .SeedAsync(db, CancellationToken);
        var historical = await db
            .BiOracleStatMappings.AsNoTracking()
            .OrderBy(m => m.Id)
            .Select(m => new
            {
                m.Id,
                m.MappingSetId,
                m.SubCategoryMetricId,
                m.TargetTable,
                m.TargetColumn,
            })
            .ToArrayAsync(CancellationToken);
        await MappingSeeder(BiOracleStatMappingSeedData.Definitions, clock).SeedAsync(db, CancellationToken);
        await MappingSeeder(BiOracleStatMappingSeedData.Definitions, clock).SeedAsync(db, CancellationToken);
        var sets = await db.BiOracleStatMappingSets.OrderBy(s => s.Id).ToArrayAsync(CancellationToken);
        Assert.Equal(2, sets.Length);
        Assert.Equal(expectedInitialDate, sets[0].EffectiveDate);
        Assert.Equal(new DateOnly(2026, 10, 31), sets[0].ExpiryDate);
        Assert.Equal(new DateOnly(2026, 11, 1), sets[1].EffectiveDate);
        Assert.Equal(sets[0].ExpiryDate!.Value.AddDays(1), sets[1].EffectiveDate);
        Assert.Equal(1, sets[1].EffectiveDate.Day);
        Assert.Null(sets[1].ExpiryDate);
        Assert.Equal(
            historical,
            await db
                .BiOracleStatMappings.AsNoTracking()
                .Where(m => m.MappingSetId == 1)
                .OrderBy(m => m.Id)
                .Select(m => new
                {
                    m.Id,
                    m.MappingSetId,
                    m.SubCategoryMetricId,
                    m.TargetTable,
                    m.TargetColumn,
                })
                .ToArrayAsync(CancellationToken)
        );
        Assert.Equal(661, await db.BiOracleStatMappings.CountAsync(m => m.MappingSetId == 2, CancellationToken));

        await using var fresh = CreateContext();
        await SeedTaxonomyAsync(fresh);
        await MappingSeeder(BiOracleStatMappingSeedData.Definitions, clock).SeedAsync(fresh, CancellationToken);
        Assert.Equal(2, await fresh.BiOracleStatMappingSets.CountAsync(CancellationToken));
    }

    private static BiOracleStatMappingSetSeedDefinition Expanded =>
        BiOracleStatMappingSeedData.Definitions.Single(d => d.Id == 2);

    private static int[] Sources(string column) =>
        Expanded.Mappings.Where(m => m.TargetColumn == column).Select(m => m.SubCategoryMetricId).Order().ToArray();

    private static async Task SeedTaxonomyAsync(UnifiedDbContext db)
    {
        await new StatGroupSeeder(NullLogger<StatGroupSeeder>.Instance).SeedAsync(db, CancellationToken);
        await new StatCategorySeeder(NullLogger<StatCategorySeeder>.Instance).SeedAsync(db, CancellationToken);
        await new SubCategorySeeder(NullLogger<SubCategorySeeder>.Instance).SeedAsync(db, CancellationToken);
        await new StatMetricSeeder(NullLogger<StatMetricSeeder>.Instance).SeedAsync(db, CancellationToken);
        await new SubCategoryMetricSeeder(NullLogger<SubCategoryMetricSeeder>.Instance).SeedAsync(
            db,
            CancellationToken
        );
    }

    private static BiOracleStatMappingSeeder MappingSeeder(
        IReadOnlyList<BiOracleStatMappingSetSeedDefinition> definitions,
        TimeProvider clock
    ) =>
        new(
            NullLogger<BiOracleStatMappingSeeder>.Instance,
            [new BiOracleStatMappingSeedConfiguration { Source = "Test", Definitions = definitions }],
            clock
        );

    private static UnifiedDbContext CreateContext() =>
        new(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseInMemoryDatabase($"LegacyStats-{Guid.NewGuid()}").Options
        );

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
