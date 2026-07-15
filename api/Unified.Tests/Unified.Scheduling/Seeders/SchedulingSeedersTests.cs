using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Unified.Authorization;
using Unified.Db;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.UserManagement;
using Unified.Scheduling.Seeders;
using Unified.Tests.TestHelpers;

namespace Unified.Tests.Scheduling.Seeders;

public sealed class SchedulingSeedersTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private UnifiedDbContext _dbContext = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.CreateFunction("now", () => DateTimeOffset.UtcNow.ToString("O"));
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite(_connection).Options;
        _dbContext = new SqliteTestUnifiedDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task AssignmentLookupSeeder_SeedAsync_UsesAssignmentCategoryCodesAndIsIdempotent()
    {
        _dbContext.AssignmentCategoryTypes.Add(
            new AssignmentCategoryType
            {
                Code = "CourtRoom",
                Description = "Old description",
                EffectiveDate = new DateTimeOffset(2019, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ExpiryDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var seeder = new AssignmentLookupSeeder(new NullLogger<AssignmentLookupSeeder>());

        await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);
        await seeder.SeedAsync(_dbContext, TestContext.Current.CancellationToken);

        var categoryTypes = await _dbContext
            .AssignmentCategoryTypes.OrderBy(type => type.Code)
            .Select(type => new
            {
                type.Code,
                type.Description,
                type.EffectiveDate,
                type.ExpiryDate,
            })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            ["CourtRole", "CourtRoom", "EscortRun", "JailRole", "OtherAssignment"],
            categoryTypes.Select(type => type.Code).ToArray()
        );
        Assert.Contains(categoryTypes, type => type.Code == "CourtRoom" && type.Description == "Court Room");

        var subCategoryTypes = await _dbContext
            .AssignmentSubCategoryTypes.OrderBy(type => type.Code)
            .Select(type => new
            {
                type.Code,
                type.Description,
                type.EffectiveDate,
                type.ExpiryDate,
                ParentCode = type.ParentCodeType.Code,
            })
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(categoryTypes, type => type.Code == "CourtRole" && type.Description == "Court Assignment");
        Assert.Contains(categoryTypes, type => type.Code == "JailRole" && type.Description == "Jail Assignment");
        Assert.Contains(categoryTypes, type => type.Code == "EscortRun" && type.Description == "Transport Assignment");
        Assert.Contains(
            categoryTypes,
            type => type.Code == "OtherAssignment" && type.Description == "Other Assignment"
        );
        Assert.All(
            categoryTypes,
            type =>
            {
                Assert.Equal(new DateTimeOffset(2020, 6, 10, 0, 0, 0, TimeSpan.Zero), type.EffectiveDate);
                Assert.Null(type.ExpiryDate);
            }
        );

        Assert.Equal(
            ["IN_CUSTODY", "OTHER", "OUT_OF_CUSTODY", "PROVINCIAL", "SUPREME"],
            subCategoryTypes.Select(type => type.Code).ToArray()
        );
        Assert.Contains(
            subCategoryTypes,
            type => type.Code == "PROVINCIAL" && type.Description == "Provincial" && type.ParentCode == "CourtRoom"
        );
        Assert.Contains(
            subCategoryTypes,
            type => type.Code == "SUPREME" && type.Description == "Supreme" && type.ParentCode == "CourtRoom"
        );
        Assert.Contains(
            subCategoryTypes,
            type => type.Code == "IN_CUSTODY" && type.Description == "In custody" && type.ParentCode == "EscortRun"
        );
        Assert.Contains(
            subCategoryTypes,
            type =>
                type.Code == "OUT_OF_CUSTODY"
                && type.Description == "Out of custody"
                && type.ParentCode == "EscortRun"
        );
        Assert.Contains(
            subCategoryTypes,
            type => type.Code == "OTHER" && type.Description == "Other" && type.ParentCode == "OtherAssignment"
        );
        Assert.All(
            subCategoryTypes,
            type =>
            {
                Assert.Equal(new DateTimeOffset(2020, 6, 10, 0, 0, 0, TimeSpan.Zero), type.EffectiveDate);
                Assert.Null(type.ExpiryDate);
            }
        );
    }

    private static Permission CreatePermission(string id) =>
        new()
        {
            Id = id,
            Group = "Scheduling",
            Description = id,
        };
}
