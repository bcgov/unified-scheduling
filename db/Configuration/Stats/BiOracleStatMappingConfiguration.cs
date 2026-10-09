using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Stats;

namespace Unified.Db.Configuration.Stats;

public class BiOracleStatMappingConfiguration : BaseEntityConfiguration<BiOracleStatMapping>
{
    public override void Configure(EntityTypeBuilder<BiOracleStatMapping> builder)
    {
        builder.ToTable("BiOracleStatMappings", "etl");

        builder
            .HasOne(mapping => mapping.MappingSet)
            .WithMany(mappingSet => mappingSet.Mappings)
            .HasForeignKey(mapping => mapping.MappingSetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(mapping => mapping.SubCategoryMetric)
            .WithMany()
            .HasForeignKey(mapping => mapping.SubCategoryMetricId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(mapping => mapping.TargetTable).HasMaxLength(30);
        builder.Property(mapping => mapping.TargetColumn).HasMaxLength(30);

        builder.HasIndex(mapping => mapping.MappingSetId);
        builder.HasIndex(mapping => mapping.SubCategoryMetricId);
        builder
            .HasIndex(mapping => new
            {
                mapping.MappingSetId,
                mapping.SubCategoryMetricId,
                mapping.TargetTable,
                mapping.TargetColumn,
            })
            .IsUnique();

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_BiOracleStatMappings_TargetTable",
                "\"TargetTable\" IN ('SHERIFF_SRVCES', 'SHERIFF_SRVCES_HOURS')"
            )
        );
        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_BiOracleStatMappings_TargetColumn",
                "length(trim(\"TargetColumn\")) > 0"
            )
        );

        base.Configure(builder);
    }
}
