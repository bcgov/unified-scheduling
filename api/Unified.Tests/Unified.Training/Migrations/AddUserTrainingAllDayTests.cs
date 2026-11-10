using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Unified.Db.Migrations;
using Unified.Db.Models.Training;

namespace Unified.Tests.Training.Migrations;

public sealed class AddUserTrainingAllDayTests
{
    [Fact]
    public async Task Up_LegacyRowsBecomeAllDay_PreservesDatesAndAllowsExplicitTimedRows()
    {
        await using var db = new DbContext(new DbContextOptionsBuilder().UseSqlite("Data Source=:memory:").Options);
        var cancellationToken = TestContext.Current.CancellationToken;
        await db.Database.OpenConnectionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "UserTrainings" ("Id" INTEGER PRIMARY KEY, "AwardedOn" TEXT NOT NULL, "EndingOn" TEXT NOT NULL);
            INSERT INTO "UserTrainings" VALUES (1, '2026-07-20T15:00:00Z', '2026-07-21T18:00:00Z');
            """,
            cancellationToken
        );
        var migration = new AddUserTrainingAllDay();
        var operation = Assert.IsType<AddColumnOperation>(Assert.Single(migration.UpOperations));
        Assert.False(operation.IsNullable);
        Assert.True(Assert.IsType<bool>(operation.DefaultValue));
        Assert.True(new UserTraining().AllDay);

        foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations))
            await db.Database.ExecuteSqlRawAsync(command.CommandText, cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "UserTrainings" ("Id", "AwardedOn", "EndingOn", "AllDay")
            VALUES (2, '2026-07-20T22:00:00Z', '2026-07-21T01:00:00Z', 0);
            """,
            cancellationToken
        );
        await using var query = db.Database.GetDbConnection().CreateCommand();
        query.CommandText = """SELECT "AllDay", "AwardedOn", "EndingOn" FROM "UserTrainings" ORDER BY "Id";""";
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);

        Assert.True(await reader.ReadAsync(cancellationToken));
        Assert.True(reader.GetBoolean(0));
        Assert.Equal("2026-07-20T15:00:00Z", reader.GetString(1));
        Assert.Equal("2026-07-21T18:00:00Z", reader.GetString(2));
        Assert.True(await reader.ReadAsync(cancellationToken));
        Assert.False(reader.GetBoolean(0));
        Assert.Equal("2026-07-20T22:00:00Z", reader.GetString(1));
        Assert.Equal("2026-07-21T01:00:00Z", reader.GetString(2));
        Assert.False(await reader.ReadAsync(cancellationToken));
    }
}
