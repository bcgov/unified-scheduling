using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Unified.Db.Models.Scheduling;

namespace Unified.Db.Configuration.Scheduling;

public class ShiftAssignmentEntryConfiguration : BaseEntityConfiguration<ShiftAssignmentEntry>
{
    public override void Configure(EntityTypeBuilder<ShiftAssignmentEntry> builder)
    {
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 200);

        builder
            .HasOne(b => b.ShiftEntry)
            .WithMany(b => b.ShiftAssignmentEntries)
            .HasForeignKey(b => b.ShiftEntryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(b => b.AssignmentEntry)
            .WithMany(b => b.ShiftAssignmentEntries)
            .HasForeignKey(b => b.AssignmentEntryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(b => b.ShiftAssignmentSeriesLink)
            .WithMany(b => b.EntryLinks)
            .HasForeignKey(b => b.ShiftAssignmentSeriesLinkId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.AssignmentEntryId);
        builder.HasIndex(b => b.ShiftAssignmentSeriesLinkId);
        builder.HasIndex(b => new { b.ShiftEntryId, b.AssignmentEntryId }).IsUnique();

        builder.ToTable("ShiftAssignmentEntries");

        base.Configure(builder);
    }
}
