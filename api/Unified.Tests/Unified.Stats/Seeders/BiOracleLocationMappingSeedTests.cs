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

        Assert.Equal(118, definitions.Count);
        Assert.Equal(118, definitions.Select(mapping => mapping.JustinLocationCode).Distinct().Count());
        Assert.Equal(118, definitions.Select(mapping => mapping.CrtLocId).Distinct().Count());
        Assert.All(
            definitions,
            mapping =>
            {
                Assert.False(string.IsNullOrWhiteSpace(mapping.JustinLocationCode));
                Assert.Equal(4, mapping.JustinLocationCode.Length);
            }
        );
        Assert.Contains(definitions, mapping => mapping.JustinLocationCode == "5831" && mapping.CrtLocId == 127);
    }

    [Fact]
    public async Task SeedAsync_ValidDefinitions_PersistsAndUpdatesMappings()
    {
        await using var context = CreateContext();
        var configuration = Configuration(
            new BiOracleLocationMappingSeedDefinition
            {
                Id = 1,
                JustinLocationCode = "1201",
                CrtLocId = 38,
            }
        );
        var seeder = Seeder(configuration);

        await seeder.SeedAsync(context);
        await seeder.SeedAsync(context);

        var mapping = await context.BiOracleLocationMappings.SingleAsync();
        Assert.Equal("1201", mapping.JustinLocationCode);
        Assert.Equal(38, mapping.CrtLocId);
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
                },
                new BiOracleLocationMappingSeedDefinition
                {
                    Id = 2,
                    JustinLocationCode = "1201",
                    CrtLocId = 39,
                }
            )
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync(context));
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
