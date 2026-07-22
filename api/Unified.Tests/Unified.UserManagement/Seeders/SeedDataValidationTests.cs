using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models;
using Unified.Tests.TestHelpers;
using Unified.UserManagement;
using Unified.UserManagement.Seeders;

namespace Unified.Tests.UserManagement.Seeders;

public sealed class SeedDataValidationTests
{
    [Fact]
    public async Task RegionSeeder_ExistingRegion_PreservesJustinIdWhileUpdatingSeedControlledFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(connection).Options;
        await using var dbContext = new SqliteTestUnifiedDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        dbContext.Regions.Add(
            new Region
            {
                Id = 100,
                JustinId = 42,
                Code = "OLD",
                Name = "Old",
            }
        );
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var seeder = new RegionSeeder(
            new NullLogger<RegionSeeder>(),
            [
                new RegionSeedConfiguration
                {
                    Source = "test",
                    Regions =
                    [
                        new()
                        {
                            Id = 100,
                            Code = "NEW",
                            Name = "New",
                        },
                    ],
                },
            ]
        );
        await seeder.SeedAsync(dbContext, TestContext.Current.CancellationToken);

        var region = await dbContext.Regions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(42, region.JustinId);
        Assert.Equal("NEW", region.Code);
        Assert.Equal("New", region.Name);
    }

    [Fact]
    public async Task SheriffRegionLocationData_CanSeedCleanDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(connection).Options;
        await using var dbContext = new SqliteTestUnifiedDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await new RegionSeeder(
            new NullLogger<RegionSeeder>(),
            [
                new RegionSeedConfiguration
                {
                    Source = "sheriff-region-location-data",
                    Regions = SheriffRegionSeedData.Regions,
                },
            ]
        ).SeedAsync(dbContext, TestContext.Current.CancellationToken);
        await new LocationSeeder(
            new NullLogger<LocationSeeder>(),
            [
                new LocationSeedConfiguration
                {
                    Source = "sheriff-region-location-data",
                    Locations = SheriffLocationSeedData.Locations,
                },
            ]
        ).SeedAsync(dbContext, TestContext.Current.CancellationToken);

        Assert.Equal(
            SheriffRegionSeedData.Regions.Count,
            await dbContext.Regions.CountAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(
            SheriffLocationSeedData.Locations.Count,
            await dbContext.Locations.CountAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task UserSeeder_DuplicateIdirNameAcrossDataSets_ThrowsIgnoringCase()
    {
        var seeder = new UserSeeder(
            new NullLogger<UserSeeder>(),
            [
                new UserSeedConfiguration
                {
                    Source = "one",
                    Users =
                    [
                        new()
                        {
                            Id = Guid.NewGuid(),
                            IdirName = "JDoe",
                            IsEnabled = true,
                            FirstName = "Jane",
                            LastName = "Doe",
                        },
                    ],
                },
                new UserSeedConfiguration
                {
                    Source = "two",
                    Users =
                    [
                        new()
                        {
                            Id = Guid.NewGuid(),
                            IdirName = "jdoe",
                            IsEnabled = true,
                            FirstName = "John",
                            LastName = "Doe",
                        },
                    ],
                },
            ]
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            seeder.SeedAsync(null!, TestContext.Current.CancellationToken)
        );
        Assert.Contains("IdirName", exception.Message);
        Assert.Contains("one", exception.Message);
        Assert.Contains("two", exception.Message);
    }

    [Fact]
    public async Task RoleSeeder_DuplicateNameAcrossDataSets_ThrowsIgnoringCase()
    {
        var seeder = new RoleSeeder(
            new NullLogger<RoleSeeder>(),
            [
                new RoleSeedConfiguration
                {
                    Source = "one",
                    Roles =
                    [
                        new()
                        {
                            Id = 1,
                            Name = "Manager",
                            Description = "",
                        },
                    ],
                },
                new RoleSeedConfiguration
                {
                    Source = "two",
                    Roles =
                    [
                        new()
                        {
                            Id = 2,
                            Name = "manager",
                            Description = "",
                        },
                    ],
                },
            ]
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            seeder.SeedAsync(null!, TestContext.Current.CancellationToken)
        );
        Assert.Contains("Name", exception.Message);
    }

    [Fact]
    public async Task RegionSeeder_DuplicateCodeAcrossDataSets_ThrowsIgnoringCase()
    {
        var seeder = new RegionSeeder(
            new NullLogger<RegionSeeder>(),
            [
                new RegionSeedConfiguration
                {
                    Source = "one",
                    Regions =
                    [
                        new()
                        {
                            Id = 1,
                            Code = "CP",
                            Name = "Central",
                        },
                    ],
                },
                new RegionSeedConfiguration
                {
                    Source = "two",
                    Regions =
                    [
                        new()
                        {
                            Id = 2,
                            Code = "cp",
                            Name = "Central Two",
                        },
                    ],
                },
            ]
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            seeder.SeedAsync(null!, TestContext.Current.CancellationToken)
        );
        Assert.Contains("Code", exception.Message);
    }

    [Fact]
    public async Task LocationSeeder_DuplicateAgencyIdAcrossDataSets_ThrowsIgnoringCase()
    {
        var seeder = new LocationSeeder(
            new NullLogger<LocationSeeder>(),
            [
                new LocationSeedConfiguration
                {
                    Source = "one",
                    Locations =
                    [
                        new()
                        {
                            Id = 1,
                            AgencyId = "SS1",
                            Name = "One",
                            Timezone = "America/Vancouver",
                        },
                    ],
                },
                new LocationSeedConfiguration
                {
                    Source = "two",
                    Locations =
                    [
                        new()
                        {
                            Id = 2,
                            AgencyId = "ss1",
                            Name = "Two",
                            Timezone = "America/Vancouver",
                        },
                    ],
                },
            ]
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            seeder.SeedAsync(null!, TestContext.Current.CancellationToken)
        );
        Assert.Contains("AgencyId", exception.Message);
    }

    [Fact]
    public async Task PermissionSeeder_DuplicateIdAcrossDataSets_ThrowsIgnoringCase()
    {
        var seeder = new PermissionSeeder(
            new NullLogger<PermissionSeeder>(),
            [
                new PermissionSeedConfiguration
                {
                    Source = "one",
                    Permissions =
                    [
                        new()
                        {
                            Id = "UsersView",
                            Group = "Users",
                            Description = "",
                        },
                    ],
                },
                new PermissionSeedConfiguration
                {
                    Source = "two",
                    Permissions =
                    [
                        new()
                        {
                            Id = "usersview",
                            Group = "Users",
                            Description = "",
                        },
                    ],
                },
            ]
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            seeder.SeedAsync(null!, TestContext.Current.CancellationToken)
        );
        Assert.Contains("one", exception.Message);
        Assert.Contains("two", exception.Message);
    }
}
