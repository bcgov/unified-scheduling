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

        builder.HasOne(b => b.EventSeries).WithMany().HasForeignKey(b => b.EventSeriesId).OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(b => b.AssignmentCategoryType)
            .WithMany()
            .HasForeignKey(b => b.AssignmentCategoryTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(b => b.AssignmentSubCategoryType)
            .WithMany()
            .HasForeignKey(b => b.AssignmentSubCategoryTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.AssignmentType).WithMany().HasForeignKey(b => b.AssignmentTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.EventSeriesId).IsUnique();
        builder.HasIndex(b => b.AssignmentCategoryTypeId);
        builder.HasIndex(b => b.AssignmentSubCategoryTypeId);
        builder.HasIndex(b => b.AssignmentTypeId);

        builder.ToTable(
            "AssignmentSeries",
            table => table.HasCheckConstraint("CK_AssignmentSeries_CapacityAtLeastOne", "\"Capacity\" >= 1")
        );

        base.Configure(builder);
    }
}
