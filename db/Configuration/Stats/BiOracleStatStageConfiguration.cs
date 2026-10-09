using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Stats;

namespace Unified.Db.Configuration.Stats;

public class BiOracleStatStageConfiguration : IEntityTypeConfiguration<BiOracleStatStage>
{
    public void Configure(EntityTypeBuilder<BiOracleStatStage> builder)
    {
        builder.ToTable("BiOracleStatStages", "etl");

        builder
            .HasOne(stage => stage.EtlRun)
            .WithMany(run => run.StagedCells)
            .HasForeignKey(stage => stage.LoadId)
            .HasPrincipalKey(run => run.LoadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(stage => stage.TargetTable).HasMaxLength(30);
        builder.Property(stage => stage.TargetColumn).HasMaxLength(30);
        builder.Property(stage => stage.StatValue).HasColumnType("numeric(18,4)");

        builder.HasIndex(stage => stage.LoadId);
        builder
            .HasIndex(stage => new
            {
                stage.LoadId,
                stage.ReportingMonth,
                stage.CrtLocId,
                stage.IsLocationLevel,
                stage.IsSupervisor,
                stage.TargetTable,
                stage.TargetColumn,
            })
            .IsUnique();

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_BiOracleStatStages_LocationLevelSupervisor",
                "NOT \"IsLocationLevel\" OR NOT \"IsSupervisor\""
            )
        );
        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_BiOracleStatStages_TargetTable",
                "\"TargetTable\" IN ('SHERIFF_SRVCES', 'SHERIFF_SRVCES_HOURS')"
            )
        );
        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_BiOracleStatStages_TargetColumn",
                "length(trim(\"TargetColumn\")) > 0"
            )
        );
        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_BiOracleStatStages_SourceRowCount",
                "\"SourceRowCount\" > 0"
            )
        );
    }
}
