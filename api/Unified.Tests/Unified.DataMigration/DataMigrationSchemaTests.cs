using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Unified.Db;
using Unified.Db.Migrations;
using Unified.Db.Models.DataMigration;

namespace Unified.Tests.DataMigration;

public sealed class DataMigrationSchemaTests
{
    [Fact]
    public void Watermark_LastCompletedRunRelationship_IsOptionalRestrictiveAndIndexed()
    {
        using var db = new UnifiedDbContext(
            new DbContextOptionsBuilder<UnifiedDbContext>().UseSqlite("DataSource=:memory:").Options
        );
        var entityType = db.Model.FindEntityType(typeof(DataMigrationWatermark));
        var foreignKey = Assert.Single(entityType!.GetForeignKeys());

        Assert.Equal(typeof(DataMigrationRun), foreignKey.PrincipalEntityType.ClrType);
        Assert.False(foreignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(nameof(DataMigrationWatermark.LastCompletedRunId), foreignKey.Properties.Single().Name);
        Assert.Contains(
            entityType.GetIndexes(),
            index =>
                index
                    .Properties.Select(property => property.Name)
                    .SequenceEqual([nameof(DataMigrationWatermark.LastCompletedRunId)])
        );
    }

    [Fact]
    public void AddDataMigrationState_CreatesWatermarkRunForeignKeyAndIndex()
    {
        var operations = new TestableAddDataMigrationState().GetUpOperations();
        var watermarks = Assert.Single(
            operations.OfType<CreateTableOperation>(),
            operation => operation.Name == "DataMigrationWatermarks"
        );
        var foreignKey = Assert.Single(
            watermarks.ForeignKeys,
            constraint => constraint.Name == "FK_DataMigrationWatermarks_DataMigrationRuns_LastCompletedRunId"
        );

        Assert.Equal("DataMigrationRuns", foreignKey.PrincipalTable);
        Assert.Equal("Id", foreignKey.PrincipalColumns!.Single());
        Assert.Equal("LastCompletedRunId", foreignKey.Columns.Single());
        Assert.Equal(ReferentialAction.Restrict, foreignKey.OnDelete);
        Assert.Contains(
            operations.OfType<CreateIndexOperation>(),
            operation =>
                operation.Name == "IX_DataMigrationWatermarks_LastCompletedRunId"
                && operation.Table == "DataMigrationWatermarks"
                && operation.Columns.SequenceEqual(["LastCompletedRunId"])
        );
    }

    private sealed class TestableAddDataMigrationState : AddDataMigrationState
    {
        public IReadOnlyList<MigrationOperation> GetUpOperations()
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            Up(builder);
            return builder.Operations;
        }
    }
}
