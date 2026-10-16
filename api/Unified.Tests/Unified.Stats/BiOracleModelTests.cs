using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Stats;

namespace Unified.Tests.Stats;

public sealed class BiOracleModelTests
{
    private readonly IModel _model = new UnifiedDbContext(
        new DbContextOptionsBuilder<UnifiedDbContext>().UseNpgsql("Host=localhost;Database=model-tests").Options
    ).Model;

    [Fact]
    public void Model_MapsAllBiOracleEntitiesToEtlSchema()
    {
        Assert.Equal("etl", Entity<BiOracleLocationMapping>().GetSchema());
        Assert.Equal("etl", Entity<BiOracleStatMappingSet>().GetSchema());
        Assert.Equal("etl", Entity<BiOracleStatMapping>().GetSchema());
        Assert.Equal("etl", Entity<BiOracleEtlRun>().GetSchema());
        Assert.Equal("etl", Entity<BiOracleStatStage>().GetSchema());
    }

    [Fact]
    public void LocationMapping_HasUniqueCodesAndNoLocationRelationship()
    {
        var entity = Entity<BiOracleLocationMapping>();
        var uniqueIndexes = entity.GetIndexes().Where(index => index.IsUnique).ToArray();

        Assert.Contains(
            uniqueIndexes,
            index => PropertyNames(index).SequenceEqual([nameof(BiOracleLocationMapping.JustinLocationCode)])
        );
        Assert.Contains(
            uniqueIndexes,
            index => PropertyNames(index).SequenceEqual([nameof(BiOracleLocationMapping.CrtLocId)])
        );
        Assert.DoesNotContain(
            entity.GetForeignKeys(),
            foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Location)
        );
        Assert.True(typeof(BaseEntity).IsAssignableFrom(typeof(BiOracleLocationMapping)));
    }

    [Fact]
    public void Mapping_HasExactTupleUniquenessWithoutRestrictingSupportedCardinalities()
    {
        var entity = Entity<BiOracleStatMapping>();
        var uniqueIndexes = entity.GetIndexes().Where(index => index.IsUnique).ToArray();

        Assert.Contains(
            uniqueIndexes,
            index =>
                PropertyNames(index)
                    .SequenceEqual([
                        nameof(BiOracleStatMapping.MappingSetId),
                        nameof(BiOracleStatMapping.SubCategoryMetricId),
                        nameof(BiOracleStatMapping.TargetTable),
                        nameof(BiOracleStatMapping.TargetColumn),
                    ])
        );
        Assert.DoesNotContain(
            uniqueIndexes,
            index => PropertyNames(index).SequenceEqual([nameof(BiOracleStatMapping.SubCategoryMetricId)])
        );
        Assert.DoesNotContain(
            uniqueIndexes,
            index =>
                PropertyNames(index)
                    .SequenceEqual([
                        nameof(BiOracleStatMapping.MappingSetId),
                        nameof(BiOracleStatMapping.TargetTable),
                        nameof(BiOracleStatMapping.TargetColumn),
                    ])
        );
    }

    [Fact]
    public void EtlRunAndStage_UseUuidLoadIdAndUniqueDestinationCellGrain()
    {
        var run = Entity<BiOracleEtlRun>();
        var stage = Entity<BiOracleStatStage>();
        var errorMessage = run.FindProperty(nameof(BiOracleEtlRun.ErrorMessage));

        Assert.Equal("uuid", run.FindProperty(nameof(BiOracleEtlRun.LoadId)).GetColumnType());
        Assert.NotNull(errorMessage);
        Assert.True(errorMessage.IsNullable);
        Assert.Equal("text", errorMessage.GetColumnType());
        Assert.Equal("uuid", stage.FindProperty(nameof(BiOracleStatStage.LoadId)).GetColumnType());
        Assert.Contains(
            run.GetKeys(),
            key =>
                !key.IsPrimaryKey()
                && key.Properties.Select(property => property.Name).SequenceEqual([nameof(BiOracleEtlRun.LoadId)])
        );
        Assert.Contains(
            stage.GetIndexes().Where(index => index.IsUnique),
            index =>
                PropertyNames(index)
                    .SequenceEqual([
                        nameof(BiOracleStatStage.LoadId),
                        nameof(BiOracleStatStage.ReportingMonth),
                        nameof(BiOracleStatStage.CrtLocId),
                        nameof(BiOracleStatStage.IsLocationLevel),
                        nameof(BiOracleStatStage.IsSupervisor),
                        nameof(BiOracleStatStage.TargetTable),
                        nameof(BiOracleStatStage.TargetColumn),
                    ])
        );
        Assert.Contains(
            stage.GetCheckConstraints(),
            constraint => constraint.Name == "CK_BiOracleStatStages_LocationLevelSupervisor"
        );
    }

    [Fact]
    public void Stage_MatchesSourcePrecisionAndContainsNoPiiAuditProperties()
    {
        var sourceValue = Entity<StatRecord>().FindProperty(nameof(StatRecord.Value));
        var stage = Entity<BiOracleStatStage>();
        var stageValue = stage.FindProperty(nameof(BiOracleStatStage.StatValue));

        Assert.Equal(sourceValue.GetColumnType(), stageValue.GetColumnType());
        Assert.Equal(typeof(bool), stage.FindProperty(nameof(BiOracleStatStage.IsLocationLevel)).ClrType);
        Assert.Equal(typeof(bool), stage.FindProperty(nameof(BiOracleStatStage.IsSupervisor)).ClrType);
        Assert.False(typeof(BaseEntity).IsAssignableFrom(typeof(BiOracleStatStage)));
        Assert.DoesNotContain(
            stage.GetProperties(),
            property =>
                property.Name.Contains("User", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Comment", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("CreatedBy", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("UpdatedBy", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Relationships_PreserveMappingHistoryAndCascadeRunStageCleanup()
    {
        var mappingForeignKeys = Entity<BiOracleStatMapping>().GetForeignKeys().ToArray();
        var runMappingSetForeignKey = Entity<BiOracleEtlRun>()
            .GetForeignKeys()
            .Single(foreignKey =>
                foreignKey.Properties.Any(property => property.Name == nameof(BiOracleEtlRun.MappingSetId))
            );
        var stageRunForeignKey = Entity<BiOracleStatStage>()
            .GetForeignKeys()
            .Single(foreignKey =>
                foreignKey.Properties.Any(property => property.Name == nameof(BiOracleStatStage.LoadId))
            );

        Assert.All(mappingForeignKeys, foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        Assert.Equal(DeleteBehavior.Restrict, runMappingSetForeignKey.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, stageRunForeignKey.DeleteBehavior);
    }

    private IMutableEntityType Entity<TEntity>() => (IMutableEntityType)_model.FindEntityType(typeof(TEntity))!;

    private static IEnumerable<string> PropertyNames(IReadOnlyIndex index) =>
        index.Properties.Select(property => property.Name);
}
