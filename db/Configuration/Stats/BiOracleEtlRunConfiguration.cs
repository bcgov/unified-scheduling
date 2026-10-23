using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Stats;

namespace Unified.Db.Configuration.Stats;

public class BiOracleEtlRunConfiguration : BaseEntityConfiguration<BiOracleEtlRun>
{
    public override void Configure(EntityTypeBuilder<BiOracleEtlRun> builder)
    {
        builder.ToTable("BiOracleEtlRuns", "etl");

        builder.HasAlternateKey(run => run.LoadId);
        builder
            .HasOne(run => run.MappingSet)
            .WithMany(mappingSet => mappingSet.EtlRuns)
            .HasForeignKey(run => run.MappingSetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(run => run.Status).HasMaxLength(20);
        builder.Property(run => run.ErrorMessage).HasColumnType("text");

        builder.HasIndex(run => run.MappingSetId);
        builder.HasIndex(run => run.ReportingMonth);
        builder.HasIndex(run => run.StartedAt);

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_BiOracleEtlRuns_Status",
                "\"Status\" IN ('Running', 'Succeeded', 'Failed', 'Skipped')"
            )
        );
        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_BiOracleEtlRuns_Counts",
                "\"SourceRecordCount\" >= 0 AND \"StagedCellCount\" >= 0 AND \"LoadedCellCount\" >= 0"
            )
        );

        base.Configure(builder);
    }
}
