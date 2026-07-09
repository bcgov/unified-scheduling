using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Scheduling;

namespace Unified.Db.Configuration.Scheduling;

public class AssignmentEntryConfiguration : BaseEntityConfiguration<AssignmentEntry>
{
    public override void Configure(EntityTypeBuilder<AssignmentEntry> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);
        builder.Property(b => b.Capacity).IsRequired();

        builder
            .HasOne(b => b.AssignmentSeries)
            .WithMany(b => b.AssignmentEntries)
            .HasForeignKey(b => b.AssignmentSeriesId)
            .OnDelete(DeleteBehavior.SetNull);
        builder
            .HasOne(b => b.Event)
            .WithMany()
            .HasForeignKey(b => b.EventId)
            .OnDelete(DeleteBehavior.Restrict);
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
        builder
            .HasOne(b => b.AssignmentType)
            .WithMany()
            .HasForeignKey(b => b.AssignmentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.AssignmentSeriesId);
        builder.HasIndex(b => b.EventId).IsUnique();
        builder.HasIndex(b => b.AssignmentCategoryTypeId);
        builder.HasIndex(b => b.AssignmentSubCategoryTypeId);
        builder.HasIndex(b => b.AssignmentTypeId);

        builder.ToTable(
            "AssignmentEntries",
            table =>
                table.HasCheckConstraint(
                    "CK_AssignmentEntries_CapacityAtLeastOne",
                    "\"Capacity\" >= 1"
                )
        );

        base.Configure(builder);
    }
}
