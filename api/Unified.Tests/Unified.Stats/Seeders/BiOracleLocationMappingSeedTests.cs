using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Db;
using Unified.Stats.Seeders;

namespace Unified.Tests.Stats.Seeders;

public sealed class BiOracleLocationMappingSeedTests
{
    [Fact]
    public void ProductionData_HasCompleteUniqueLocationMappings()
    {
        var definitions = BiOracleLocationMappingSeedData.Definitions;

        Assert.Equal(119, definitions.Count);
        Assert.Equal(119, definitions.Select(mapping => mapping.JustinLocationCode).Distinct().Count());
        Assert.Equal(119, definitions.Select(mapping => mapping.CrtLocId).Distinct().Count());
        Assert.All(
            definitions,
            mapping =>
            {
                Assert.False(string.IsNullOrWhiteSpace(mapping.JustinLocationCode));
                Assert.Equal(4, mapping.JustinLocationCode.Length);
                Assert.Equal(mapping.JustinLocationCode.Trim(), mapping.JustinLocationCode);
            }
        );
        Assert.Contains(definitions, mapping => mapping.JustinLocationCode == "5831" && mapping.CrtLocId == 127);
        Assert.Contains(
            definitions,
            mapping => mapping.Id == 119 && mapping.JustinLocationCode == "1131" && mapping.CrtLocId == 310
        );
    }

    [Theory]
    [InlineData("1201")]
    [InlineData("aB12")]
    public async Task SeedAsync_ValidDefinitions_PersistsAndUpdatesMappings(string justinLocationCode)
    {
        await using var context = CreateContext();
        var configuration = Configuration(
            new BiOracleLocationMappingSeedDefinition
            {
                Id = 1,
                JustinLocationCode = justinLocationCode,
                CrtLocId = 38,
                EffectiveDate = new DateOnly(2000, 1, 1),
                ExpiryDate = new DateOnly(2026, 1, 1),
            }
        );
        var seeder = Seeder(configuration);

        await seeder.SeedAsync(context, TestContext.Current.CancellationToken);
        await seeder.SeedAsync(context, TestContext.Current.CancellationToken);

        var mapping = await context.BiOracleLocationMappings.SingleAsync(
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(justinLocationCode, mapping.JustinLocationCode);
        Assert.Equal(38, mapping.CrtLocId);
        Assert.Equal(new DateOnly(2000, 1, 1), mapping.EffectiveDate);
        Assert.Equal(new DateOnly(2026, 1, 1), mapping.ExpiryDate);
    }

    [Fact]
    public async Task SeedAsync_ProductionData_PreservesAllDatesOnRerun()
    {
        await using var context = CreateContext();
        var definitions = BiOracleLocationMappingSeedData.Definitions;
        var seeder = Seeder(Configuration(definitions.ToArray()));

        await seeder.SeedAsync(context, TestContext.Current.CancellationToken);
        await seeder.SeedAsync(context, TestContext.Current.CancellationToken);

        var mappings = await context
            .BiOracleLocationMappings.AsNoTracking()
            .OrderBy(mapping => mapping.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            definitions.Select(mapping => (mapping.Id, mapping.EffectiveDate, mapping.ExpiryDate)),
            mappings.Select(mapping => (mapping.Id, mapping.EffectiveDate, mapping.ExpiryDate))
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("120")]
    [InlineData("12011")]
    [InlineData(" 120")]
    [InlineData("120 ")]
    [InlineData(" 1201")]
    [InlineData("1201 ")]
    public async Task SeedAsync_InvalidJustinLocationCode_RejectsBeforeChangingMappings(string? justinLocationCode)
    {
        await using var context = CreateContext();
        var original = new BiOracleLocationMappingSeedDefinition
        {
            Id = 1,
            JustinLocationCode = "1201",
            CrtLocId = 38,
            EffectiveDate = new DateOnly(2000, 1, 1),
        };
        await Seeder(Configuration(original)).SeedAsync(context, TestContext.Current.CancellationToken);
        var seeder = Seeder(
            Configuration(
                original with
                {
                    CrtLocId = 39,
                },
                original with
                {
                    Id = 2,
                    JustinLocationCode = "1202",
                    CrtLocId = 40,
                },
                original with
                {
                    Id = 3,
                    JustinLocationCode = justinLocationCode!,
                    CrtLocId = 41,
                }
            )
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            seeder.SeedAsync(context, TestContext.Current.CancellationToken)
        );

        Assert.Contains("four-character JUSTIN location code", exception.Message);
        var mapping = Assert.Single(
            await context.BiOracleLocationMappings.AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(38, mapping.CrtLocId);
        Assert.Equal(original.EffectiveDate, mapping.EffectiveDate);
        Assert.False(context.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task SeedAsync_DuplicateJustinLocationCode_Throws()
    {
        await using var context = CreateContext();
        var seeder = Seeder(
            Configuration(
                new BiOracleLocationMappingSeedDefinition
                {
                    Id = 1,
                    JustinLocationCode = "1201",
                    CrtLocId = 38,
                    EffectiveDate = new DateOnly(2000, 1, 1),
                },
                new BiOracleLocationMappingSeedDefinition
                {
                    Id = 2,
                    JustinLocationCode = "1201",
                    CrtLocId = 39,
                    EffectiveDate = new DateOnly(2000, 1, 1),
                }
            )
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            seeder.SeedAsync(context, TestContext.Current.CancellationToken)
        );
    }

    private static BiOracleLocationMappingSeeder Seeder(
        params BiOracleLocationMappingSeedConfiguration[] configurations
    ) => new(NullLogger<BiOracleLocationMappingSeeder>.Instance, configurations);

    private static BiOracleLocationMappingSeedConfiguration Configuration(
        params BiOracleLocationMappingSeedDefinition[] definitions
    ) => new() { Source = "Test", Definitions = definitions };

    private static UnifiedDbContext CreateContext() =>
        new(
            new DbContextOptionsBuilder<UnifiedDbContext>()
                .UseInMemoryDatabase($"BiOracleLocationMappingSeed-{Guid.NewGuid()}")
                .Options
        );
}
