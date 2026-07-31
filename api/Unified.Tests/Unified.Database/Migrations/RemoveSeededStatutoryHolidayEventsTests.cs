using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Unified.Db.Migrations;

namespace Unified.Tests.Database.Migrations;

public sealed class RemoveSeededStatutoryHolidayEventsTests
{
    [Fact]
    public void Up_DeletesOnlyRowsMatchingKnownLegacySeedValues()
    {
        var sql = GetUpSql();

        Assert.Contains("USING (", sql, StringComparison.Ordinal);
        Assert.Contains("('Canada Day', TIMESTAMPTZ '2026-07-01 00:00:00+00'", sql, StringComparison.Ordinal);
        Assert.Contains("('Christmas Day', TIMESTAMPTZ '2027-12-25 00:00:00+00'", sql, StringComparison.Ordinal);
        Assert.Contains("event.\"Title\" = legacy.\"Title\"", sql, StringComparison.Ordinal);
        Assert.Contains("event.\"StartAtUtc\" = legacy.\"StartAtUtc\"", sql, StringComparison.Ordinal);
        Assert.Contains("event.\"EndAtUtc\" = legacy.\"EndAtUtc\"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Up_PreservesCustomizedOrUserAttributedHolidayRows()
    {
        var sql = GetUpSql();

        Assert.Contains("event.\"EventSeriesId\" IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("event.\"Description\" IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("event.\"Notes\" IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("event.\"Color\" IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("event.\"TimeZoneId\" IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("event.\"LocationId\" IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("event.\"CreatedById\" IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("event.\"UpdatedById\" IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("event.\"UpdatedOn\" IS NULL", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Down_DoesNotRecreateRetiredSeedRows()
    {
        var migration = new TestableRemoveSeededStatutoryHolidayEvents();

        Assert.Empty(migration.GetDownOperations());
    }

    private static string GetUpSql()
    {
        var migration = new TestableRemoveSeededStatutoryHolidayEvents();
        var operation = Assert.IsType<SqlOperation>(Assert.Single(migration.GetUpOperations()));
        return operation.Sql;
    }

    private sealed class TestableRemoveSeededStatutoryHolidayEvents : RemoveSeededStatutoryHolidayEvents
    {
        public IReadOnlyList<MigrationOperation> GetUpOperations()
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            Up(builder);
            return builder.Operations;
        }

        public IReadOnlyList<MigrationOperation> GetDownOperations()
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            Down(builder);
            return builder.Operations;
        }
    }
}
