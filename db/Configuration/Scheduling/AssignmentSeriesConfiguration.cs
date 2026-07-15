using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Scheduling;

namespace Unified.Db.Configuration.Scheduling;

public class AssignmentSeriesConfiguration : BaseEntityConfiguration<AssignmentSeries>
{
    public override void Configure(EntityTypeBuilder<AssignmentSeries> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);
        builder.Property(b => b.Capacity).IsRequired();

        builder
            .HasOne(b => b.AssignmentDefinition)
            .WithMany(b => b.AssignmentSeries)
            .HasForeignKey(b => b.AssignmentDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(b => b.EventSeries)
            .WithMany()
            .HasForeignKey(b => b.EventSeriesId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(b => b.EventSeriesId).IsUnique();
        builder.HasIndex(b => b.AssignmentDefinitionId);

        builder.ToTable(
            "AssignmentSeries",
            table =>
                table.HasCheckConstraint(
                    "CK_AssignmentSeries_CapacityAtLeastOne",
                    "\"Capacity\" >= 1"
                )
        );

        base.Configure(builder);
    }
}
